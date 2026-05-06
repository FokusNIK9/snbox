
Add-Type -TypeDefinition '
using System;
using System.Runtime.InteropServices;
public class Win32 {
    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
}'
$proc = Get-Process sbox-dev | Where-Object { $_.MainWindowTitle -like "*editor*" } | Select-Object -First 1
if ($proc) {
    $hWnd = $proc.MainWindowHandle
    $rect = New-Object Win32+RECT
    [Win32]::GetWindowRect($hWnd, [ref]$rect)
    $rect
} else {
    "NOTFOUND"
}
