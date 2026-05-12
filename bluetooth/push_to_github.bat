@echo off
REM Batch script to push bluetooth code to GitHub repository VR_cyclingtask
REM Make sure Git is installed and configured before running this script

echo ========================================
echo Pushing Bluetooth Code to GitHub
echo Repository: VR_cyclingtask
echo ========================================
echo.

REM Check if git is available
git --version >nul 2>&1
if errorlevel 1 (
    echo ERROR: Git is not installed or not in PATH
    echo Please install Git from: https://git-scm.com/download/win
    echo Make sure to select "Add Git to PATH" during installation
    pause
    exit /b 1
)

echo [OK] Git is installed
echo.

REM Navigate to bluetooth directory
cd /d "%~dp0"
echo Current directory: %CD%
echo.

REM Check if .git exists
if exist .git (
    echo [OK] Git repository already initialized
) else (
    echo Initializing Git repository...
    git init
    if errorlevel 1 (
        echo ERROR: Failed to initialize git repository
        pause
        exit /b 1
    )
    echo [OK] Git repository initialized
)

echo.
echo Adding remote repository...
git remote remove origin 2>nul
git remote add origin https://github.com/YOUR_USERNAME/VR_cyclingtask.git
if errorlevel 1 (
    echo.
    echo WARNING: Could not add remote. You may need to:
    echo 1. Replace YOUR_USERNAME with your GitHub username
    echo 2. Or use SSH: git@github.com:YOUR_USERNAME/VR_cyclingtask.git
    echo.
    echo Please edit this script and replace YOUR_USERNAME
    pause
    exit /b 1
)

echo [OK] Remote repository added
echo.

echo Adding files...
git add .
if errorlevel 1 (
    echo ERROR: Failed to add files
    pause
    exit /b 1
)
echo [OK] Files added
echo.

echo Committing changes...
git commit -m "Add Bluetooth bike and HR monitor connection with pedaling detection and comprehensive data plotting"
if errorlevel 1 (
    echo WARNING: No changes to commit or commit failed
    echo This is OK if files are already committed
)
echo.

echo Pushing to GitHub...
echo Note: You may be prompted for GitHub credentials
echo.
git push -u origin main
if errorlevel 1 (
    echo.
    echo Trying 'master' branch instead...
    git push -u origin master
    if errorlevel 1 (
        echo.
        echo ERROR: Failed to push to GitHub
        echo Possible issues:
        echo 1. Authentication failed - use GitHub Personal Access Token
        echo 2. Branch name mismatch - check your repository default branch
        echo 3. Network issues
        echo.
        echo To generate a Personal Access Token:
        echo https://github.com/settings/tokens
        pause
        exit /b 1
    )
)

echo.
echo ========================================
echo [OK] Successfully pushed to GitHub!
echo Repository: VR_cyclingtask
echo ========================================
pause
