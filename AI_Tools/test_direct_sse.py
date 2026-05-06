import requests
import json
import threading
import time
import sys

SSE_URL = "http://localhost:8098/sse"

def test_direct_sse():
    print("=" * 60)
    print("  Direct SSE Test to s&box MCP Server")
    print("=" * 60)
    
    # 1. Подключаемся к SSE
    print(f"\n[1] Connecting to SSE: {SSE_URL}")
    try:
        response = requests.get(SSE_URL, stream=True, headers={'Accept': 'text/event-stream'}, timeout=10)
        response.raise_for_status()
        print(f"    Status: {response.status_code}")
        print(f"    Content-Type: {response.headers.get('Content-Type')}")
    except Exception as e:
        print(f"    FAILED: {e}")
        return
    
    # 2. Читаем endpoint
    print("\n[2] Reading SSE events for endpoint...")
    post_endpoint = None
    lines_iter = response.iter_lines()
    
    for line in lines_iter:
        if line:
            decoded = line.decode('utf-8')
            print(f"    SSE: {decoded}")
            if decoded.startswith("data: "):
                endpoint_data = decoded[len("data: "):]
                if endpoint_data.startswith("http"):
                    post_endpoint = endpoint_data
                else:
                    from urllib.parse import urljoin
                    post_endpoint = urljoin(SSE_URL, endpoint_data)
                break
    
    if not post_endpoint:
        print("    FAILED: No endpoint received")
        return
    
    print(f"    Endpoint: {post_endpoint}")
    
    # 3. Отправляем initialize
    print("\n[3] Sending initialize...")
    init_payload = {
        "jsonrpc": "2.0",
        "id": 1,
        "method": "initialize",
        "params": {
            "protocolVersion": "2024-11-05",
            "capabilities": {},
            "clientInfo": {"name": "test", "version": "1.0"}
        }
    }
    
    try:
        r = requests.post(post_endpoint, json=init_payload, timeout=10)
        print(f"    POST status: {r.status_code}")
    except Exception as e:
        print(f"    FAILED: {e}")
        return
    
    # 4. Читаем ответ из SSE
    print("\n[4] Reading initialize response from SSE...")
    for line in lines_iter:
        if line:
            decoded = line.decode('utf-8')
            print(f"    SSE: {decoded[:300]}")
            if decoded.startswith("data: "):
                try:
                    data = json.loads(decoded[len("data: "):])
                    if data.get("id") == 1:
                        print(f"\n    Initialize result keys: {list(data.get('result', {}).keys())}")
                        break
                except json.JSONDecodeError:
                    pass
    
    # 5. Отправляем notifications/initialized
    print("\n[5] Sending notifications/initialized...")
    notif = {
        "jsonrpc": "2.0",
        "method": "notifications/initialized"
    }
    try:
        r = requests.post(post_endpoint, json=notif, timeout=10)
        print(f"    POST status: {r.status_code}")
    except Exception as e:
        print(f"    FAILED: {e}")
    
    # 6. Отправляем tools/list
    print("\n[6] Sending tools/list...")
    tools_payload = {
        "jsonrpc": "2.0",
        "id": 2,
        "method": "tools/list",
        "params": {}
    }
    try:
        r = requests.post(post_endpoint, json=tools_payload, timeout=10)
        print(f"    POST status: {r.status_code}")
    except Exception as e:
        print(f"    FAILED: {e}")
        return
    
    # 7. Читаем ответ tools/list
    print("\n[7] Reading tools/list response from SSE...")
    for line in lines_iter:
        if line:
            decoded = line.decode('utf-8')
            print(f"    SSE: {decoded[:500]}")
            if decoded.startswith("data: "):
                try:
                    data = json.loads(decoded[len("data: "):])
                    if data.get("id") == 2:
                        tools = data.get("result", {}).get("tools", [])
                        print(f"\n    Tools count: {len(tools)}")
                        if tools:
                            print(f"    First 5 tools: {[t['name'] for t in tools[:5]]}")
                        break
                except json.JSONDecodeError:
                    pass
    
    # 8. Отправляем tools/call get_scene_summary
    print("\n[8] Sending tools/call(get_scene_summary)...")
    call_payload = {
        "jsonrpc": "2.0",
        "id": 3,
        "method": "tools/call",
        "params": {
            "name": "get_scene_summary",
            "arguments": {}
        }
    }
    try:
        r = requests.post(post_endpoint, json=call_payload, timeout=10)
        print(f"    POST status: {r.status_code}")
    except Exception as e:
        print(f"    FAILED: {e}")
        return
    
    # 9. Читаем ответ
    print("\n[9] Reading get_scene_summary response from SSE...")
    for line in lines_iter:
        if line:
            decoded = line.decode('utf-8')
            print(f"    SSE: {decoded[:500]}")
            if decoded.startswith("data: "):
                try:
                    data = json.loads(decoded[len("data: "):])
                    if data.get("id") == 3:
                        result = data.get("result")
                        if result:
                            content = result.get("content", [])
                            if content:
                                print(f"\n    Scene summary text: {content[0].get('text', 'N/A')[:300]}")
                        break
                except json.JSONDecodeError:
                    pass
    
    print("\n" + "=" * 60)
    print("  Test Complete")
    print("=" * 60)

if __name__ == "__main__":
    test_direct_sse()
