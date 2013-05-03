@echo off

setlocal

set WIX="c:\Program Files (x86)\WiX Toolset v3.7\bin"

%WIX%\candle.exe -out Setup.wixobj -ext WixUtilExtension -ext WixUIExtension -nologo Setup.wxs
%WIX%\light.exe -o TundraVS2012.msi -ext WixUtilExtension -ext WixUIExtension -cultures:en-us Setup.wixobj

del Setup.wixobj
del TundraVS2012.wixpdb
