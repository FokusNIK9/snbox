import subprocess
import json
import sys
import time
import threading

def test_mcp_bridge():
    print("=" * 60)
    print("  MCP Bridge Test")
    print("=" * 60)
    
    # Запускаем sbox_mcp_bridge.py как subprocess
    import os
    script_dir = os.path.dirname(os.path.abspath(__file__))
    bridge_path = os.path.join(script_dir, "sbox_mcp_bridge.py")
    
    proc = subprocess.Popen(
        [sys.executable, bridge_path],
        stdin=subprocess.PIPE,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
        encoding='utf-8'
    )
    
    stdout_lines = []
    stderr_lines = []
    
    def read_stdout():
        for line in proc.stdout:
            line = line.strip()
            if line:
                stdout_lines.append(line)
                print(f"[STDOUT] {line[:200]}")
    
    def read_stderr():
        for line in proc.stderr:
            line = line.strip()
            if line:
                stderr_lines.append(line)
                print(f"[STDERR] {line}")
    
    t_out = threading.Thread(target=read_stdout, daemon=True)
    t_err = threading.Thread(target=read_stderr, daemon=True)
    t_out.start()
    t_err.start()
    
    # Ждём пока bridge установит SSE соединение
    print("\n[TEST] Waiting for SSE connection...")
    time.sleep(2)
    
    # Отправляем initialize запрос
    init_request = {
        "jsonrpc": "2.0",
        "id": 1,
        "method": "initialize",
        "params": {
            "protocolVersion": "2024-11-05",
            "capabilities": {},
            "clientInfo": {"name": "test", "version": "1.0"}
        }
    }
    
    print(f"\n[TEST] Sending initialize request...")
    proc.stdin.write(json.dumps(init_request) + "\n")
    proc.stdin.flush()
    
    # Ждём ответа
    time.sleep(3)
    
    # Отправляем notifications/initialized
    notif = {
        "jsonrpc": "2.0",
        "method": "notifications/initialized"
    }
    print(f"\n[TEST] Sending notifications/initialized...")
    proc.stdin.write(json.dumps(notif) + "\n")
    proc.stdin.flush()
    
    time.sleep(1)
    
    # Отправляем tools/list запрос
    tools_request = {
        "jsonrpc": "2.0",
        "id": 2,
        "method": "tools/list",
        "params": {}
    }
    
    print(f"\n[TEST] Sending tools/list request...")
    proc.stdin.write(json.dumps(tools_request) + "\n")
    proc.stdin.flush()
    
    # Ждём ответа
    time.sleep(3)
    
    # Отправляем tools/call запрос
    call_request = {
        "jsonrpc": "2.0",
        "id": 3,
        "method": "tools/call",
        "params": {
            "name": "get_scene_summary",
            "arguments": {}
        }
    }
    
    print(f"\n[TEST] Sending tools/call(get_scene_summary) request...")
    proc.stdin.write(json.dumps(call_request) + "\n")
    proc.stdin.flush()
    
    # Ждём ответа
    time.sleep(3)
    
    # Закрываем stdin
    proc.stdin.close()
    
    # Ждём завершения
    print("\n[TEST] Waiting for process to finish...")
    try:
        proc.wait(timeout=5)
    except subprocess.TimeoutExpired:
        print("[TEST] Process didn't finish in time, killing...")
        proc.kill()
        proc.wait()
    
    print("\n" + "=" * 60)
    print("  Test Results")
    print("=" * 60)
    print(f"Total stdout lines: {len(stdout_lines)}")
    print(f"Total stderr lines: {len(stderr_lines)}")
    print(f"Return code: {proc.returncode}")
    
    # Парсим ответы
    for i, line in enumerate(stdout_lines):
        try:
            data = json.loads(line)
            if "id" in data:
                print(f"\n[RESPONSE id={data.get('id')}] method result keys: {list(data.get('result', {}).keys()) if data.get('result') else 'NO RESULT'}")
                if data.get('error'):
                    print(f"[ERROR] {data['error']}")
        except json.JSONDecodeError:
            print(f"\n[RAW LINE {i}] {line[:200]}")
    
    print("\n" + "=" * 60)

if __name__ == "__main__":
    test_mcp_bridge()
