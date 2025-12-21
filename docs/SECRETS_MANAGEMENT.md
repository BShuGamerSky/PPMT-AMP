# Environment Secrets Management

## 🎯 Strategy: Separate Secrets Per Environment

### Why Not Use Production Secret in Development?

**❌ Problems:**
- Secret rotation breaks all developers
- More exposure = more risk
- Hard to onboard new developers
- Testing secret rotation is impossible

**✅ Solution: Environment-Specific Secrets**

---

## 📦 Current Setup (Development)

### What You Have Now
```
Secret: "your-secret-key-change-this-in-production"
Status: ✅ SAFE for development
Committed: YES (in appsettings.json and .env.development)
Purpose: Local development, debugging, testing
```

### Keep It This Way!
- All developers use same dev secret
- Easy to set up new environments
- Safe to share in team chat
- No secret management overhead

---

## 🚀 When You're Ready for Production

### Step 1: Keep Development As-Is
```bash
# Development (keep current setup)
./scripts/deploy-production.sh development
```
- Uses known dev secret
- No changes needed
- Continue developing

### Step 2: Add Staging Environment
```bash
# Staging (generates new secret automatically)
./scripts/deploy-production.sh staging
```
- Generates unique staging secret
- Stores in AWS Secrets Manager
- Never touches development

### Step 3: Deploy Production
```bash
# Production (generates unique production secret)
./scripts/deploy-production.sh production
```
- Generates unique prod secret (different from staging)
- Stores in AWS Secrets Manager
- Never touches development or staging

---

## 📊 Environment Comparison

| Aspect | Development | Staging | Production |
|--------|-------------|---------|------------|
| **Secret** | Known fixed value | Generated random | Generated random |
| **In Git** | ✅ YES | ❌ NO | ❌ NO |
| **Storage** | Config file | AWS Secrets Manager | AWS Secrets Manager |
| **Rotation** | Never | Quarterly | Monthly |
| **Access** | All developers | DevOps + QA | DevOps only |
| **AWS Account** | Dev/Sandbox | Staging account | Production account |

---

## 🛠️ Daily Development Workflow

### For You (Developer)
```bash
# 1. Clone repo
git clone <repo>

# 2. That's it! Config already has dev secret
dotnet build

# 3. Run app
dotnet run
```

**No secret management needed for development!**

### For Production Deployment (Later)
```bash
# Only when deploying to production
./scripts/deploy-production.sh production

# Script automatically:
# - Generates secure random secret
# - Stores in AWS Secrets Manager  
# - Updates Lambda environment
# - Keeps dev environment unchanged
```

---

## 🔒 Security Benefits

### Development Secret Compromised?
- ✅ No problem! Only affects dev environment
- ✅ Production unaffected
- ✅ Just change dev secret in config
- ✅ Git commit updates everyone

### Production Secret Compromised?
- ✅ Rotate prod secret immediately
- ✅ Dev environment keeps working
- ✅ No developer coordination needed
- ✅ Emergency fix deployed quickly

---

## 💡 Best Practices

### DO ✅
- Use known dev secret for local development
- Commit dev config to git
- Generate unique secrets for staging/prod
- Store prod secrets in AWS Secrets Manager
- Rotate prod secrets regularly

### DON'T ❌
- Use production secret in development
- Commit staging/prod secrets to git
- Share production secrets in Slack/email
- Reuse secrets across environments
- Hardcode secrets in code

---

## 🔄 Secret Rotation

### Development (Annual or Never)
```bash
# Update dev secret if needed
# 1. Change in config/appsettings.json
# 2. Update Lambda: ./scripts/deploy-production.sh development
# 3. Commit to git
# 4. Team pulls latest
```

### Production (Monthly)
```bash
# 1. Generate new secret
NEW_SECRET=$(openssl rand -base64 32)

# 2. Update Secrets Manager
aws secretsmanager update-secret \
  --secret-id ppmt-amp/production/app-secret \
  --secret-string "$NEW_SECRET"

# 3. Deploy Lambda
./scripts/deploy-production.sh production

# 4. Update iOS app config
# (in CI/CD pipeline, auto-fetches from Secrets Manager)

# 5. Deploy app update
```

---

## 📝 Summary

**Your Current Setup is Perfect for Development:**
- ✅ Simple known secret
- ✅ Committed to git
- ✅ Easy for all developers
- ✅ No production risk

**When deploying to production:**
- Use `./scripts/deploy-production.sh production`
- Script handles everything automatically
- Development remains unchanged
- No additional complexity for daily work

**Bottom Line:**
Keep your current dev secret! Only generate production secret when you actually deploy to production. This gives you zero friction in development with maximum security in production.
