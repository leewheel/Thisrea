Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public class W2 {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int n);
  [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr h);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr h, int x, int y, int w, int ht, bool repaint);
  public delegate bool EnumProc(IntPtr h, IntPtr l);
}
"@
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$procs = Get-Process ReaGui -ErrorAction SilentlyContinue
if (-not $procs) { "NO PROCESS"; exit }
$target = [uint32]$procs[0].Id

$wins = New-Object System.Collections.ArrayList
$cb = [W2+EnumProc]{
  param($h, $l)
  $op = [uint32]0
  [void][W2]::GetWindowThreadProcessId($h, [ref]$op)
  if ($op -eq $target -and [W2]::IsWindowVisible($h)) {
    $sb = New-Object System.Text.StringBuilder 256
    [void][W2]::GetWindowText($h, $sb, 256)
    [void]$wins.Add([pscustomobject]@{ H=$h; T=$sb.ToString() })
  }
  return $true
}
[void][W2]::EnumWindows($cb, [IntPtr]::Zero)
foreach ($d in $wins) { "win [$($d.T)]" }

$main = ($wins | Where-Object { $_.T -eq "REA GUI" } | Select-Object -First 1).H
if (-not $main) { $main = $wins[0].H }
[W2]::ShowWindow($main, 9) | Out-Null
[W2]::MoveWindow($main, 40, 20, 1500, 900, $true) | Out-Null
[W2]::BringWindowToTop($main) | Out-Null
[W2]::SetForegroundWindow($main) | Out-Null
Start-Sleep -Seconds 1

$b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$bmp = New-Object System.Drawing.Bitmap($b.Width, $b.Height)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($b.Location, [System.Drawing.Point]::Empty, $b.Size)
$out = "D:\REA\Thisrea\ReaGui\Assets\shot.png"
$bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
"MAIN=$main saved"
