"""
ChatGPT <-> Ozmium MCP Bridge for S&box
========================================
Запускает REST API сервер, который ChatGPT Actions может вызывать.
Перенаправляет запросы в Ozmium MCP Server (localhost:8098).

Использование:
  1. Убедитесь что s&box запущен и Ozmium MCP Server активен (порт 8098)
  2. Установите зависимости:
       pip install fastapi uvicorn requests
  3. Запустите:
       python chatgpt_mcp_proxy.py
  4. Откройте туннель:
       ngrok http 8001
  5. В ChatGPT -> Configure -> Actions -> Import URL:
       https://ваш-ngrok-url/openapi.json

Сервер автоматически обнаруживает все инструменты Ozmium MCP и создаёт
REST API + OpenAPI схему для ChatGPT.
"""

import argparse
import json
import threading
import time
import sys
import requests as req_lib
from urllib.parse import urljoin
from typing import Any, Dict, List, Optional

# --- Конфигурация ---
MCP_SSE_URL = "http://localhost:8098/sse"
PROXY_PORT = 8001
PROXY_HOST = "0.0.0.0"
SERVER_URL = None  # Публичный URL (ngrok), задаётся через --server-url

# =====================================================
# MCP Client — подключение к Ozmium MCP Server по SSE
# =====================================================

class McpClient:
	"""Клиент для Ozmium MCP Server через SSE."""

	def __init__(self, sse_url: str = MCP_SSE_URL):
		self.sse_url = sse_url
		self.tools_cache: List[dict] = []

	def _connect_and_call(self, method: str, params: dict, timeout: float = 10.0) -> dict:
		"""Открывает SSE соединение, отправляет JSON-RPC запрос, ждёт ответ."""
		response = req_lib.get(
			self.sse_url,
			stream=True,
			headers={"Accept": "text/event-stream"},
			timeout=30
		)
		response.raise_for_status()

		post_endpoint = None
		lines_iter = response.iter_lines()

		for line in lines_iter:
			if line:
				decoded = line.decode("utf-8")
				if decoded.startswith("data: "):
					endpoint_data = decoded[len("data: "):]
					if endpoint_data.startswith("http"):
						post_endpoint = endpoint_data
					else:
						post_endpoint = urljoin(self.sse_url, endpoint_data)
					break

		if not post_endpoint:
			raise ConnectionError("Не удалось получить endpoint от MCP SSE")

		result_holder = {}
		error_holder = {}

		def read_sse():
			try:
				for line in lines_iter:
					if line:
						decoded = line.decode("utf-8")
						if decoded.startswith("data: "):
							data = decoded[len("data: "):]
							parsed = json.loads(data)
							if "error" in parsed:
								error_holder["error"] = parsed["error"]
							else:
								result_holder["result"] = parsed.get("result", parsed)
							break
			except Exception as e:
				error_holder["error"] = {"message": str(e)}

		reader = threading.Thread(target=read_sse, daemon=True)
		reader.start()

		payload = {
			"jsonrpc": "2.0",
			"method": method,
			"params": params,
			"id": 1
		}
		req_lib.post(post_endpoint, json=payload, timeout=30)
		reader.join(timeout=timeout)

		if error_holder:
			raise RuntimeError(f"MCP error: {error_holder['error']}")
		if not result_holder:
			raise TimeoutError("Таймаут ожидания ответа от MCP")

		return result_holder["result"]

	def list_tools(self) -> List[dict]:
		"""Получает список всех MCP инструментов."""
		result = self._connect_and_call("tools/list", {})
		self.tools_cache = result.get("tools", [])
		return self.tools_cache

	def call_tool(self, tool_name: str, arguments: dict) -> dict:
		"""Вызывает MCP инструмент по имени."""
		result = self._connect_and_call(
			"tools/call",
			{"name": tool_name, "arguments": arguments},
			timeout=15.0
		)
		# Извлекаем текстовый контент из MCP ответа
		if "content" in result:
			for item in result["content"]:
				if item.get("type") == "text":
					try:
						return json.loads(item["text"])
					except (json.JSONDecodeError, TypeError):
						return {"text": item["text"]}
		return result


# =====================================================
# FastAPI REST API — для ChatGPT Actions
# =====================================================

try:
	from fastapi import FastAPI, HTTPException, Request
	from fastapi.middleware.cors import CORSMiddleware
	from fastapi.responses import JSONResponse
except ImportError:
	print("Установите FastAPI: pip install fastapi uvicorn")
	sys.exit(1)

mcp = McpClient()

def create_app(server_url: Optional[str] = None) -> "FastAPI":
	servers = [{"url": server_url}] if server_url else None
	_app = FastAPI(
		title="Ozmium S&box MCP Bridge",
		description=(
			"REST API мост между ChatGPT и S&box редактором через Ozmium MCP Server. "
			"Позволяет читать сцену, создавать/изменять объекты, управлять editor'ом и многое другое. "
			"Используйте GET /tools для получения списка доступных инструментов, "
			"затем POST /call_tool для вызова нужного инструмента."
		),
		version="1.0.0",
		servers=servers,
	)
	return _app

app = create_app()

app.add_middleware(
	CORSMiddleware,
	allow_origins=["*"],
	allow_credentials=True,
	allow_methods=["*"],
	allow_headers=["*"],
)


@app.get(
	"/tools",
	summary="Список всех MCP инструментов",
	description=(
		"Возвращает полный список доступных инструментов Ozmium MCP Server "
		"с именами, описаниями и параметрами. Вызови это ПЕРВЫМ чтобы узнать "
		"какие инструменты доступны."
	),
)
def get_tools():
	try:
		tools = mcp.list_tools()
		simplified = []
		for t in tools:
			schema = t.get("inputSchema", {})
			props = schema.get("properties", {})
			params = {}
			for pname, pinfo in props.items():
				params[pname] = {
					"type": pinfo.get("type", "string"),
					"description": pinfo.get("description", ""),
				}
			simplified.append({
				"name": t["name"],
				"description": t.get("description", ""),
				"parameters": params,
			})
		return {"tools": simplified, "count": len(simplified)}
	except Exception as e:
		raise HTTPException(status_code=502, detail=f"MCP connection error: {e}")


@app.post(
	"/call_tool",
	summary="Вызвать MCP инструмент",
	description=(
		"Вызывает любой инструмент Ozmium MCP Server по имени. "
		"Сначала вызови GET /tools чтобы узнать доступные инструменты и их параметры. "
		"\n\nПопулярные инструменты:\n"
		"- get_scene_summary — обзор сцены (вызови первым)\n"
		"- find_game_objects — поиск объектов по имени/тегу/компоненту\n"
		"- get_game_object_details — детали объекта по id или name\n"
		"- get_component_properties — свойства компонента\n"
		"- set_component_property — изменить свойство компонента\n"
		"- create_game_object — создать новый объект\n"
		"- add_component — добавить компонент к объекту\n"
		"- spawn_prefab — заспавнить префаб\n"
		"- run_console_command — выполнить консольную команду\n"
		"- set_transform — изменить позицию/вращение/масштаб объекта\n"
		"- toggle_play_mode — включить/выключить режим игры\n"
	),
)
async def call_tool(request: Request):
	try:
		body = await request.json()
	except Exception:
		raise HTTPException(status_code=400, detail="Invalid JSON body")

	tool_name = body.get("tool_name") or body.get("tool") or body.get("name")
	arguments = body.get("arguments") or body.get("params") or body.get("args") or {}

	if not tool_name:
		raise HTTPException(
			status_code=400,
			detail="Укажите tool_name — имя инструмента. Вызовите GET /tools для списка."
		)

	# Убираем нестроковые ключи и None значения
	clean_args = {k: v for k, v in arguments.items() if v is not None}

	try:
		result = mcp.call_tool(tool_name, clean_args)
		return {"tool": tool_name, "result": result}
	except TimeoutError:
		raise HTTPException(status_code=504, detail=f"Таймаут вызова {tool_name}")
	except RuntimeError as e:
		raise HTTPException(status_code=502, detail=str(e))
	except Exception as e:
		raise HTTPException(status_code=500, detail=f"Ошибка: {e}")


# --- Быстрые эндпоинты для самых частых операций ---

@app.get(
	"/scene/summary",
	summary="Обзор сцены",
	description="Быстрый обзор текущей сцены: объекты, компоненты, теги, префабы.",
)
def scene_summary():
	try:
		return mcp.call_tool("get_scene_summary", {})
	except Exception as e:
		raise HTTPException(status_code=502, detail=str(e))


@app.get(
	"/scene/hierarchy",
	summary="Дерево сцены",
	description="Иерархия объектов сцены. rootOnly=true для верхнего уровня.",
)
def scene_hierarchy(rootOnly: bool = True, includeDisabled: bool = True):
	try:
		return mcp.call_tool("get_scene_hierarchy", {
			"rootOnly": rootOnly,
			"includeDisabled": includeDisabled,
		})
	except Exception as e:
		raise HTTPException(status_code=502, detail=str(e))


@app.get(
	"/scene/find",
	summary="Поиск объектов",
	description="Поиск GameObjects по имени, тегу или компоненту.",
)
def find_objects(
	name: Optional[str] = None,
	tag: Optional[str] = None,
	component: Optional[str] = None,
	maxResults: int = 50,
):
	args = {"maxResults": maxResults}
	if name:
		args["nameContains"] = name
	if tag:
		args["hasTag"] = tag
	if component:
		args["hasComponent"] = component
	try:
		return mcp.call_tool("find_game_objects", args)
	except Exception as e:
		raise HTTPException(status_code=502, detail=str(e))


@app.get(
	"/scene/object/{object_id}",
	summary="Детали объекта",
	description="Получить полные детали GameObject по GUID.",
)
def object_details(object_id: str):
	try:
		return mcp.call_tool("get_game_object_details", {"id": object_id})
	except Exception as e:
		raise HTTPException(status_code=502, detail=str(e))


@app.post(
	"/scene/console",
	summary="Консольная команда",
	description="Выполнить консольную команду в s&box.",
)
async def run_console(request: Request):
	body = await request.json()
	command = body.get("command", "")
	if not command:
		raise HTTPException(status_code=400, detail="Укажите command")
	try:
		return mcp.call_tool("run_console_command", {"command": command})
	except Exception as e:
		raise HTTPException(status_code=502, detail=str(e))


@app.get(
	"/health",
	summary="Проверка соединения",
	description="Проверяет что MCP сервер доступен.",
)
def health():
	try:
		result = mcp.call_tool("get_scene_summary", {})
		return {"status": "ok", "scene": result.get("sceneName", "unknown")}
	except Exception:
		return {"status": "error", "detail": "MCP server unavailable"}


# =====================================================
# Запуск
# =====================================================

if __name__ == "__main__":
	import uvicorn

	parser = argparse.ArgumentParser(description="Ozmium MCP Bridge для ChatGPT")
	parser.add_argument("--server-url", type=str, default=None,
		help="Публичный URL (ngrok) для OpenAPI servers. Пример: https://abc.ngrok-free.app")
	parser.add_argument("--port", type=int, default=PROXY_PORT,
		help=f"Порт сервера (по умолчанию {PROXY_PORT})")
	parser.add_argument("--mcp-url", type=str, default=MCP_SSE_URL,
		help=f"URL Ozmium MCP SSE (по умолчанию {MCP_SSE_URL})")
	args = parser.parse_args()

	# Обновляем конфигурацию
	if args.mcp_url != MCP_SSE_URL:
		mcp.sse_url = args.mcp_url

	# Добавляем servers в OpenAPI схему для ChatGPT
	if args.server_url:
		def custom_openapi():
			if app.openapi_schema:
				return app.openapi_schema
			schema = original_openapi()
			schema["servers"] = [{"url": args.server_url}]
			app.openapi_schema = schema
			return schema
		original_openapi = app.openapi
		app.openapi = custom_openapi

	port = args.port

	print("=" * 60)
	print("  Ozmium MCP Bridge для ChatGPT")
	print("=" * 60)
	print(f"  MCP Server: {args.mcp_url}")
	print(f"  REST API:   http://localhost:{port}")
	print(f"  Swagger UI: http://localhost:{port}/docs")
	print(f"  OpenAPI:    http://localhost:{port}/openapi.json")
	if args.server_url:
		print(f"  Server URL: {args.server_url}")
		print()
		print(f"  В ChatGPT Actions импортируйте:")
		print(f"    {args.server_url}/openapi.json")
	else:
		print()
		print("  Следующий шаг:")
		print("    ngrok http 8001")
		print("  Затем перезапустите с --server-url:")
		print("    python chatgpt_mcp_proxy.py --server-url https://ваш-ngrok.app")
	print("=" * 60)

	uvicorn.run(app, host=PROXY_HOST, port=port)
