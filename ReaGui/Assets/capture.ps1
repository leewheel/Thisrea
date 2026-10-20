Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public class W {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int n);
  [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr h);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern int GetClassName(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
  [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr h, int x, int y, int w, int ht, bool repaint);
  public delegate bool EnumProc(IntPtr h, IntPtr l);
}
"@
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

Get-Process ReaGui -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1

$exe = "D:\REA\Thisrea\ReaGui\bin\Debug\net8.0-windows\ReaGui.exe"
$p = Start-Process -FilePath $exe -PassThru
Start-Sleep -Seconds 4
$target = [uint32]$p.Id

$wins = New-Object System.Collections.ArrayList
$cb = [W+EnumProc]{
  param($h, $l)
  $op = [uint32]0
  [void][W]::GetWindowThreadProcessId($h, [ref]$op)
  if ($op -eq $target -and [W]::IsWindowVisible($h)) {
    $sb = New-Object System.Text.StringBuilder 256
    [void][W]::GetWindowText($h, $sb, 256)
    $cn = New-Object System.Text.StringBuilder 256
    [void][W]::GetClassName($h, $cn, 256)
    [void]$wins.Add([pscustomobject]@{ H=$h; T=$sb.ToString(); C=$cn.ToString() })
  }
  return $true
}
[void][W]::EnumWindows($cb, [IntPtr]::Zero)

$main = [IntPtr]::Zero
foreach ($d in $wins) {
  "win: class=$($d.C) title=[$($d.T)]"
  if ($d.C -eq "#32770") {
    [W]::SendMessage($d.H, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null  # WM_CLOSE dialog
  } elseif ($d.T -eq "REA GUI") {
    $main = $d.H
  }
}
if ($main -eq [IntPtr]::Zero -and $wins.Count -gt 0) { $main = $wins[0].H }
Start-Sleep -Seconds 1
if ($main -ne [IntPtr]::Zero) {
  [W]::ShowWindow($main, 9) | Out-Null
  [W]::MoveWindow($main, 40, 20, 1500, 900, $true) | Out-Null
  [W]::BringWindowToTop($main) | Out-Null
  [W]::SetForegroundWindow($main) | Out-Null
}
Start-Sleep -Seconds 1

$b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$bmp = New-Object System.Drawing.Bitmap($b.Width, $b.Height)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($b.Location, [System.Drawing.Point]::Empty, $b.Size)
$out = "D:\REA\Thisrea\ReaGui\Assets\shot.png"
$bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
"MAIN=$main saved=$out"
