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
       python AI_Tools\chatgpt_mcp_proxy.py
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
	from fastapi import FastAPI, HTTPException
	from fastapi.middleware.cors import CORSMiddleware
	from pydantic import BaseModel, Field
except ImportError:
	print("Установите FastAPI: pip install fastapi uvicorn")
	sys.exit(1)


class CallToolRequest(BaseModel):
	tool_name: str = Field(..., description="Name of the MCP tool to call. Use GET /tools to see available names.")
	arguments: Dict[str, Any] = Field(default_factory=dict, description="Tool arguments as key-value pairs.")


class ConsoleRequest(BaseModel):
	command: str = Field(..., description="Console command to execute in s&box.")

mcp = McpClient()

def create_app(server_url: Optional[str] = None) -> "FastAPI":
	servers = [{"url": server_url}] if server_url else None
	_app = FastAPI(
		title="Ozmium S&box MCP Bridge",
		description="S&box editor bridge via Ozmium MCP. Use GET /tools then POST /call_tool.",
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
	summary="List all MCP tools",
	description="Returns all available Ozmium MCP tools with names, descriptions and parameters. Call this first.",
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
	summary="Call any MCP tool by name",
	description="Call any Ozmium MCP tool. Use GET /tools first to see available names.",
)
def call_tool(body: CallToolRequest):
	clean_args = {k: v for k, v in body.arguments.items() if v is not None}
	try:
		result = mcp.call_tool(body.tool_name, clean_args)
		return {"tool": body.tool_name, "result": result}
	except TimeoutError:
		raise HTTPException(status_code=504, detail=f"Timeout calling {body.tool_name}")
	except RuntimeError as e:
		raise HTTPException(status_code=502, detail=str(e))
	except Exception as e:
		raise HTTPException(status_code=500, detail=str(e))


# --- Быстрые эндпоинты для самых частых операций ---

@app.get(
	"/scene/summary",
	summary="Scene overview",
	description="Quick overview: object counts, components, tags, prefabs.",
)
def scene_summary():
	try:
		return mcp.call_tool("get_scene_summary", {})
	except Exception as e:
		raise HTTPException(status_code=502, detail=str(e))


@app.get(
	"/scene/hierarchy",
	summary="Scene hierarchy",
	description="Scene object tree. rootOnly=true for top-level only.",
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
	summary="Find objects",
	description="Search GameObjects by name, tag or component.",
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
	summary="Object details",
	description="Get full GameObject details by GUID.",
)
def object_details(object_id: str):
	try:
		return mcp.call_tool("get_game_object_details", {"id": object_id})
	except Exception as e:
		raise HTTPException(status_code=502, detail=str(e))


@app.post(
	"/scene/console",
	summary="Console command",
	description="Run a console command in s&box editor.",
)
def run_console(body: ConsoleRequest):
	try:
		return mcp.call_tool("run_console_command", {"command": body.command})
	except Exception as e:
		raise HTTPException(status_code=502, detail=str(e))


@app.get(
	"/health",
	summary="Health check",
	description="Check if MCP server is reachable.",
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
		print("    python AI_Tools\chatgpt_mcp_proxy.py --server-url https://ваш-ngrok.app")
	print("=" * 60)

	uvicorn.run(app, host=PROXY_HOST, port=port)
