import requests
import json

def call_mcp(method_name, args):
    sse_url = "http://localhost:8098/sse"
    try:
        response = requests.get(sse_url, stream=True, headers={'Accept': 'text/event-stream'})
    except Exception as e:
        print(f"Error: {e}")
        return None

    post_endpoint = None
    lines_iter = response.iter_lines()
    for line in lines_iter:
        if line:
            dec = line.decode('utf-8')
            if dec.startswith('data: '):
                ed = dec[len('data: '):]
                if not ed.startswith('http'):
                    from urllib.parse import urljoin
                    post_endpoint = urljoin(sse_url, ed)
                else:
                    post_endpoint = ed
                break

    if not post_endpoint:
        return None

    result_data = {}
    import threading
    def read_sse():
        for line in lines_iter:
            if line:
                dec = line.decode('utf-8')
                if dec.startswith('data: '):
                    data = dec[len('data: '):]
                    result_data['json'] = json.loads(data)
                    break
    
    t = threading.Thread(target=read_sse)
    t.daemon = True
    t.start()

    payload = {"jsonrpc": "2.0", "method": "tools/call", "params": {"name": method_name, "arguments": args}, "id": 1}
    requests.post(post_endpoint, json=payload)
    t.join(timeout=3)
    return result_data.get('json')

if __name__ == "__main__":
    print("Disabling loop on ParticleBoxEmitter...")
    res = call_mcp("set_component_property", {
        "name": "Floor",
        "componentType": "ParticleBoxEmitter",
        "propertyName": "Loop",
        "value": False
    })
    print(json.dumps(res, indent=2))
