import sys
import threading
import requests
import json
import time
from urllib.parse import urljoin

# Ensure stdout and stdin are using UTF-8
sys.stdout.reconfigure(encoding='utf-8')
sys.stdin.reconfigure(encoding='utf-8')

sse_url = "http://localhost:8098/sse"
post_endpoint = None

def sse_reader():
    global post_endpoint
    try:
        response = requests.get(sse_url, stream=True, headers={'Accept': 'text/event-stream'})
        response.raise_for_status()
        
        current_event = None
        for line in response.iter_lines():
            if not line:
                continue
            decoded = line.decode('utf-8')
            if decoded.startswith('event: '):
                current_event = decoded[len('event: '):]
            elif decoded.startswith('data: '):
                data_content = decoded[len('data: '):]
                if current_event == 'endpoint':
                    if data_content.startswith('http'):
                        post_endpoint = data_content
                    else:
                        post_endpoint = urljoin(sse_url, data_content)
                elif current_event == 'message':
                    # Print JSON-RPC message to stdout so the IDE can read it
                    sys.stdout.write(data_content + '\n')
                    sys.stdout.flush()
    except Exception as e:
        sys.exit(1)

# Start SSE reader thread
t = threading.Thread(target=sse_reader, daemon=True)
t.start()

# Wait until post_endpoint is obtained
while not post_endpoint:
    time.sleep(0.05)
    if not t.is_alive():
        sys.exit(1)

# Read JSON-RPC requests from stdin and forward them via POST
for line in sys.stdin:
    line = line.strip()
    if not line:
        continue
    try:
        payload = json.loads(line)
        requests.post(post_endpoint, json=payload)
    except Exception:
        pass
