# Service Tracker: Pre-built Docker Images Feature

## Overview

The `sync-project.ps1` script now builds Docker images locally on the development machine and saves them as `.tar` files. This avoids memory-intensive builds on low-memory servers (which fail with exit code 145 - OOM kill).

The Service Tracker needs to be updated to:
1. Detect when pre-built images exist in `docker-images/` folder
2. Load them using `docker load -i` before running `docker compose up`
3. Skip the `--build` flag when images are pre-loaded

---

## What sync-project.ps1 Produces

When syncing a project with Docker images, the script creates:

```
/opt/projects/<project-name>/
├── docker-compose.yml
├── docker-images/
│   ├── manifest.json
│   ├── api.tar
│   └── web.tar
└── ... (other project files)
```

### manifest.json Structure

```json
{
  "projectName": "computerinventory",
  "templateName": "Computer_Inventory",
  "builtAt": "2026-01-12T19:30:00.000Z",
  "images": [
    {
      "service": "api",
      "file": "api.tar",
      "imageName": "computerinventory-api:latest",
      "sizeMB": 212.21
    },
    {
      "service": "web",
      "file": "web.tar",
      "imageName": "computerinventory-web:latest",
      "sizeMB": 35.17
    }
  ]
}
```

---

## Required Changes in Service Tracker

### File to Modify: `packages/backend/src/index.ts`

---

### Change 1: Add Helper Function

Add this function near the top of the file (after the imports, before the route handlers):

```typescript
/**
 * Load pre-built Docker images from tar files if they exist.
 * This avoids building images on low-memory servers.
 * 
 * @param serverPath - The project's server path (e.g., /opt/projects/my-project)
 * @returns Object with arrays of loaded image names and any errors
 */
async function loadPrebuiltImages(serverPath: string): Promise<{ loaded: string[]; errors: string[] }> {
  const { exec } = await import('child_process');
  const { promisify } = await import('util');
  const { existsSync, readFileSync } = await import('fs');
  const { join } = await import('path');
  const execAsync = promisify(exec);
  
  const imagesDir = join(serverPath, 'docker-images');
  const manifestPath = join(imagesDir, 'manifest.json');
  
  const loaded: string[] = [];
  const errors: string[] = [];
  
  // Check if manifest exists
  if (!existsSync(manifestPath)) {
    console.log('ℹ️ No docker-images/manifest.json found - will build images normally');
    return { loaded, errors };
  }
  
  try {
    const manifest = JSON.parse(readFileSync(manifestPath, 'utf-8'));
    console.log(`📦 Found pre-built images manifest: ${manifest.images?.length || 0} images`);
    
    for (const imageInfo of manifest.images || []) {
      const tarPath = join(imagesDir, imageInfo.file);
      
      if (!existsSync(tarPath)) {
        errors.push(`Missing tar file: ${imageInfo.file}`);
        console.log(`⚠️ Missing tar file: ${imageInfo.file}`);
        continue;
      }
      
      try {
        console.log(`📦 Loading ${imageInfo.file} (${imageInfo.sizeMB}MB)...`);
        const { stdout, stderr } = await execAsync(`docker load -i "${tarPath}"`, { 
          timeout: 600000 // 10 minute timeout for large images
        });
        loaded.push(imageInfo.imageName);
        console.log(`✅ Loaded: ${imageInfo.imageName}`);
      } catch (loadError: any) {
        const errorMsg = `Failed to load ${imageInfo.file}: ${loadError.message}`;
        errors.push(errorMsg);
        console.error(`❌ ${errorMsg}`);
      }
    }
    
    if (loaded.length > 0) {
      console.log(`📦 Successfully loaded ${loaded.length} pre-built images`);
    }
  } catch (parseError: any) {
    errors.push(`Failed to parse manifest.json: ${parseError.message}`);
    console.error(`❌ Failed to parse manifest.json: ${parseError.message}`);
  }
  
  return { loaded, errors };
}
```

---

### Change 2: Modify the "up" / "deploy" Action

Find the section that handles `action === 'up'` or `action === 'deploy'` (around line 2258-2318).

**Before the `docker compose up` command is executed**, add the image loading step:

```typescript
// For 'up' or 'deploy' action, use docker compose CLI to create containers
if (action === 'up' || action === 'deploy') {
  console.log(`Running docker compose up in ${serverPath}`);

  const { exec } = await import('child_process');
  const { promisify } = await import('util');
  const execAsync = promisify(exec);

  // Check and auto-correct path (existing code)
  const pathInfo = await checkAndUpdatePath();

  if (!pathInfo.exists) {
    // ... existing error handling ...
  }

  // ========== NEW: Load pre-built Docker images if they exist ==========
  const imageLoadResult = await loadPrebuiltImages(pathInfo.path);
  let prebuiltOutput: string[] = [];
  
  if (imageLoadResult.loaded.length > 0) {
    prebuiltOutput.push('=== Pre-built Images Loaded ===');
    imageLoadResult.loaded.forEach(img => prebuiltOutput.push(`  ✅ ${img}`));
    prebuiltOutput.push('');
  }
  
  if (imageLoadResult.errors.length > 0) {
    prebuiltOutput.push('=== Pre-built Image Warnings ===');
    imageLoadResult.errors.forEach(err => prebuiltOutput.push(`  ⚠️ ${err}`));
    prebuiltOutput.push('');
  }
  // ========== END NEW ==========

  // ... existing secret injection code ...

  // Step 1: Stop existing containers (existing code)
  try {
    const downCommand = `cd "${pathInfo.path}" && docker compose -f "${pathInfo.file}" down -v 2>/dev/null || true`;
    // ...
  } catch (downError: any) {
    // ...
  }

  // ========== MODIFIED: Step 2 - Skip --build if images were pre-loaded ==========
  // If pre-built images were loaded successfully, don't use --build flag
  const useNoBuild = imageLoadResult.loaded.length > 0;
  const command = useNoBuild
    ? `cd "${pathInfo.path}" && docker compose -f "${pathInfo.file}" up -d --force-recreate`
    : `cd "${pathInfo.path}" && docker compose -f "${pathInfo.file}" up -d --force-recreate`;
  
  console.log(`Executing: ${command}${useNoBuild ? ' (using pre-built images)' : ''}`);
  // ========== END MODIFIED ==========

  const { stdout, stderr } = await execAsync(command, { timeout: 300000 });
  let output = stdout || stderr || 'Containers started';
  
  // ========== NEW: Prepend pre-built image info to output ==========
  if (prebuiltOutput.length > 0) {
    output = prebuiltOutput.join('\n') + '\n=== Docker Compose Output ===\n' + output;
  }
  // ========== END NEW ==========

  // ... rest of existing success handling ...
}
```

---

### Change 3: Modify the "redeploy" Action (Optional but Recommended)

Find the redeploy action (around line 2094) and add similar logic:

```typescript
// Before the redeploy command
const imageLoadResult = await loadPrebuiltImages(serverPath);

// Modify the command to skip --build if images were loaded
const useNoBuild = imageLoadResult.loaded.length > 0;
const command = useNoBuild
  ? `cd "${serverPath}" && docker compose -f "${composeFile}" up -d --force-recreate`
  : `cd "${serverPath}" && docker compose -f "${composeFile}" up -d --build --force-recreate`;

console.log(`Redeploying${useNoBuild ? ' (using pre-built images)' : ' (building images)'}...`);
```

---

## Testing the Changes

1. **Sync a project with pre-built images:**
   ```powershell
   cd C:\Users\hank\Desktop\Computer_Invintory
   .\sync-project.ps1 -TemplateName Computer_Inventory
   ```

2. **Deploy via Service Tracker UI:**
   - Go to Deployment Bay
   - Select the project
   - Click Deploy/Up

3. **Expected behavior:**
   - Console should show "📦 Found pre-built images manifest: 2 images"
   - Images should be loaded with `docker load -i`
   - `docker compose up` should run WITHOUT `--build` flag
   - Deployment should succeed without OOM errors

---

## Fallback Behavior

If no `docker-images/manifest.json` exists:
- The function returns empty arrays
- Normal build process continues with `--build` flag
- This ensures backward compatibility with projects that don't have pre-built images

---

## Summary of Changes

| Location | Change |
|----------|--------|
| Top of `index.ts` | Add `loadPrebuiltImages()` helper function |
| `action === 'up'` block (~line 2258) | Call `loadPrebuiltImages()` before docker compose up |
| `action === 'up'` block | Skip `--build` flag if images were pre-loaded |
| `action === 'up'` block | Include pre-built image info in deployment output |
| `action === 'redeploy'` block (~line 2094) | Same changes as above (optional) |

---

## Benefits

1. **Avoids OOM kills** - No memory-intensive `dotnet build` on low-memory servers
2. **Faster deployments** - Pre-built images load in seconds vs minutes to build
3. **Consistent builds** - Same image that was tested locally is deployed
4. **Backward compatible** - Projects without pre-built images still work normally
