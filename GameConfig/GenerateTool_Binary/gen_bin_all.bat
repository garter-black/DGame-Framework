@echo off
cd /d %~dp0
echo %CD%

:: 子脚本不暂停；外部已设置 AUTO_CONTINUE（AI/CI）时本脚本也不暂停，并返回第一个失败的退出码
set "GEN_OUTER_AUTO_CONTINUE=%AUTO_CONTINUE%"
set AUTO_CONTINUE=1
call gen_bin_client.bat
set "GEN_ALL_EXIT_CODE=%ERRORLEVEL%"
call gen_bin_server.bat
if "%GEN_ALL_EXIT_CODE%"=="0" set "GEN_ALL_EXIT_CODE=%ERRORLEVEL%"
set "AUTO_CONTINUE=%GEN_OUTER_AUTO_CONTINUE%"
set "GEN_OUTER_AUTO_CONTINUE="
if not defined AUTO_CONTINUE pause
exit /b %GEN_ALL_EXIT_CODE%
