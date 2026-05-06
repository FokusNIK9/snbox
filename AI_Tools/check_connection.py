import requests
import json

def test_mcp():
    url = "http://localhost:8098/sse"
    print(f"Checking Ozmium MCP at {url}...")
    try:
        # Step 1: Get the POST endpoint
        r = requests.get(url, stream=True, timeout=5)
        post_url = None
        for line in r.iter_lines():
            if line:
                decoded = line.decode('utf-8')
                if decoded.startswith('data: '):
                    data = decoded[len('data: '):]
                    if data.startswith('http'):
                        post_url = data
                    else:
                        post_url = "http://localhost:8098" + data
                    break
        
        if not post_url:
            print("Failed to get POST endpoint.")
            return

        print(f"Success! POST endpoint: {post_url}")
        
        # Step 2: Try to list tools (standard MCP)
        payload = {
            "jsonrpc": "2.0",
            "method": "tools/list",
            "params": {},
            "id": 1
        }
        resp = requests.post(post_url, json=payload)
        print(f"Server response: {resp.status_code}")
        if resp.status_code == 202:
            print("Bridge is WORKING and ready for commands.")
            
    except Exception as e:
        print(f"Error: {e}")

if __name__ == "__main__":
    test_mcp()
