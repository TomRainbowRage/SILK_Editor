@echo off

set "BASE_DIR=%~dp0"

mklink /D "%BASE_DIR%bin\Debug\net9.0\shaders" "%BASE_DIR%shaders"
mklink /D "%BASE_DIR%bin\Debug\net9.0\textures" "%BASE_DIR%textures"
mklink /D "%BASE_DIR%bin\Debug\net9.0\models" "%BASE_DIR%models"
mklink /D "%BASE_DIR%bin\Debug\net9.0\entities" "%BASE_DIR%entities"
