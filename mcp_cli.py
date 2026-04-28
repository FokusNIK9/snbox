import requests
import json
import threading
import sys

def call_mcp_tool(method_name, args):
    sse_url = "http://localhost:8098/sse"
    
    try:
        response = requests.get(sse_url, stream=True, headers={'Accept': 'text/event-stream'})
        response.raise_for_status()
    except Exception as e:
        print(f"Error connecting to SSE: {e}")
        return

    post_endpoint = None
    lines_iter = response.iter_lines()
    
    for line in lines_iter:
        if line:
            decoded_line = line.decode('utf-8')
            if decoded_line.startswith('event: endpoint'):
                continue
            elif decoded_line.startswith('data: '):
                endpoint_data = decoded_line[len('data: '):]
                if endpoint_data.startswith('http'):
                    post_endpoint = endpoint_data
                else:
                    from urllib.parse import urljoin
                    post_endpoint = urljoin(sse_url, endpoint_data)
                break

    if not post_endpoint:
        print("Did not receive an endpoint from SSE.")
        return

    def read_sse():
        for line in lines_iter:
            if line:
                decoded_line = line.decode('utf-8')
                if decoded_line.startswith('event: message'):
                    continue
                elif decoded_line.startswith('data: '):
                    data = decoded_line[len('data: '):]
                    print(json.dumps(json.loads(data), indent=2))
                    import os
                    os._exit(0)

    t = threading.Thread(target=read_sse)
    t.daemon = True
    t.start()

    payload = {
        "jsonrpc": "2.0",
        "method": "tools/call",
        "params": {
            "name": method_name,
            "arguments": args
        },
        "id": 1
    }
    
    try:
        post_response = requests.post(post_endpoint, json=payload)
        post_response.raise_for_status()
    except Exception as e:
        print(f"Error sending POST request: {e}")
            
    t.join(timeout=5)

if __name__ == "__main__":
    if len(sys.argv) < 3:
        print("Usage: python mcp_cli.py <method_name> <json_args>")
        sys.exit(1)
        
    method = sys.argv[1]
    args = json.loads(sys.argv[2])
    call_mcp_tool(method, args)
