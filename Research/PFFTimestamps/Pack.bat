@echo off
if not exist pack.exe goto wherepack
for %%f in (dump\*.*) do .\pack .\Packed.pff %%f /FORCE
pause
exit
:wherepack
echo where pack.exe
pause