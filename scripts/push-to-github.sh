#!/bin/bash
# GitHub Push Script
# Run this after creating the repository on GitHub

echo "=========================================="
echo "PPMT-AMP GitHub Setup"
echo "=========================================="
echo ""

# Check if repository URL is provided
if [ -z "$1" ]; then
    echo "Usage: ./push-to-github.sh <repository-url>"
    echo ""
    echo "Example:"
    echo "  ./push-to-github.sh https://github.com/BShuGamerSky/PPMT-AMP.git"
    echo ""
    echo "Steps to get your repository URL:"
    echo "1. Go to https://github.com/new"
    echo "2. Repository name: PPMT-AMP"
    echo "3. Description: iOS after-market price tracking app with AWS backend"
    echo "4. Click 'Create repository'"
    echo "5. Copy the HTTPS URL shown on the page"
    echo ""
    exit 1
fi

REPO_URL=$1

echo "Repository URL: $REPO_URL"
echo ""

# Add remote
echo "Adding remote 'origin'..."
git remote remove origin 2>/dev/null
git remote add origin "$REPO_URL"

# Verify remote
echo "Verifying remote..."
git remote -v

echo ""
echo "Pushing to GitHub..."
git branch -M main
git push -u origin main

echo ""
echo "=========================================="
echo "✓ Success!"
echo "=========================================="
echo ""
echo "Your repository is now live at:"
echo "${REPO_URL%.git}"
echo ""
echo "Next steps:"
echo "1. View your repository on GitHub"
echo "2. Update AWS secrets for production deployment"
echo "3. Continue development!"
echo ""
