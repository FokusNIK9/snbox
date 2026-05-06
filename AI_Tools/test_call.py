import os
import json
import urllib.request
import urllib.error

{}
API_KEY = "sk-nPMarYSntXrZqGT5s3qnzadk1u8x4-Z4loFRpUgXr9s"
BASE_URL = "https://api.zveno.ai/v1/chat/completions"
MODEL = "deepseek/deepseek-v4-pro"

messages = [
	{
		"role": "system",
		"content": "Ты полезный ассистент. Отвечай кратко и по-русски."
	}
]


def ask_zveno(message: str) -> None:
	if not API_KEY:
		print("Ошибка: не найдена переменная окружения ZVENOAI_API_KEY")
		return

	messages.append({
		"role": "user",
		"content": message
	})

	payload = {
		"model": MODEL,
		"messages": messages,
		"temperature": 0.7,
		"max_tokens": 500
	}

	data = json.dumps(payload).encode("utf-8")

	request = urllib.request.Request(
		BASE_URL,
		data=data,
		method="POST",
		headers={
			"Authorization": f"Bearer {API_KEY}",
			"Content-Type": "application/json"
		}
	)

	try:
		with urllib.request.urlopen(request, timeout=60) as response:
			result = json.loads(response.read().decode("utf-8"))

		answer = result["choices"][0]["message"]["content"]

		messages.append({
			"role": "assistant",
			"content": answer
		})

		print("\n=== Ответ модели ===\n")
		print(answer)

		print("\n=== Использование токенов ===")
		print(json.dumps(result.get("usage", {}), ensure_ascii=False, indent=2))

	except urllib.error.HTTPError as error:
		print(f"HTTP ошибка: {error.code}")
		print(error.read().decode("utf-8", errors="replace"))

		messages.pop()

	except urllib.error.URLError as error:
		print(f"Ошибка соединения: {error}")
		messages.pop()


if __name__ == "__main__":
	print("Тест ZvenoAI + DeepSeek V4 Pro")
	print("Теперь чат помнит историю.")
	print("Команды:")
	print("/exit — выход")
	print("/clear — очистить историю")

	while True:
		user_message = input("\nТы: ").strip()

		if not user_message or user_message == "/exit":
			break

		if user_message == "/clear":
			messages[:] = [
				{
					"role": "system",
					"content": "Ты полезный ассистент. Отвечай кратко и по-русски."
				}
			]
			print("История очищена.")
			continue

		ask_zveno(user_message)