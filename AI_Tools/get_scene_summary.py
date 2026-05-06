import requests
import json
import threading

def get_scene_summary():
    sse_url = "http://localhost:8098/sse"
    print(f"Connecting to {sse_url}...")
    
    # Open the SSE connection
    try:
        response = requests.get(sse_url, stream=True, headers={'Accept': 'text/event-stream'})
        response.raise_for_status()
    except Exception as e:
        print(f"Error connecting to SSE: {e}")
        return

    post_endpoint = None
    lines_iter = response.iter_lines()
    
    # Read the first message to get the endpoint
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
                print(f"Got POST endpoint: {post_endpoint}")
                break

    if not post_endpoint:
        print("Did not receive an endpoint from SSE.")
        return

    def read_sse():
        # Keep reading SSE for the response
        for line in lines_iter:
            if line:
                decoded_line = line.decode('utf-8')
                if decoded_line.startswith('event: message'):
                    continue
                elif decoded_line.startswith('data: '):
                    data = decoded_line[len('data: '):]
                    print("\n--- Response received from SSE ---")
                    try:
                        print(json.dumps(json.loads(data), indent=2))
                    except:
                        print(data)
                    # We can exit after receiving the first message
                    import os
                    os._exit(0)

    # Start SSE reader in a thread
    t = threading.Thread(target=read_sse)
    t.daemon = True
    t.start()

    # Now make the POST request
    payload = {
        "jsonrpc": "2.0",
        "method": "tools/call",
        "params": {
            "name": "get_scene_summary",
            "arguments": {}
        },
        "id": 1
    }
    
    print(f"Sending JSON-RPC request to {post_endpoint}...")
    try:
        post_response = requests.post(post_endpoint, json=payload)
        post_response.raise_for_status()
        print(f"POST request accepted (status {post_response.status_code}). Waiting for SSE response...")
    except Exception as e:
        print(f"Error sending POST request: {e}")
        if 'post_response' in locals():
            print(post_response.text)
            
    # Wait for the thread to exit the process
    t.join(timeout=5)
    print("Timed out waiting for SSE response.")

if __name__ == "__main__":
    get_scene_summary()
