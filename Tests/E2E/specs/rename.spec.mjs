import { test, expect } from '@playwright/test';
import path from 'node:path';

const appUser = process.env.E2E_APP_USER || 'admin';
const appPassword = process.env.E2E_APP_PASSWORD || 'admin';
const ftpUser = process.env.E2E_FTP_USER || 'ftpadmin';
const ftpPassword = process.env.E2E_FTP_PASSWORD || 'admin123';
const apiBaseUrl = process.env.E2E_API_URL || 'http://127.0.0.1:5230';

const sourceName = '__e2e_rename_source__.txt';
const renamedName = '__e2e_renamed__.txt';
const folderName = '__e2e_folder__';
const renamedFolderName = '__e2e_folder_renamed__';
const fixturePath = path.resolve(import.meta.dirname, '..', 'fixtures', sourceName);

async function appLogin(page) {
  await page.goto('/');
  await page.getByTestId('app-username').fill(appUser);
  await page.getByTestId('app-password').fill(appPassword);
  await page.getByTestId('app-login').click();
  await expect(page.getByTestId('ftp-login')).toBeVisible();
}

async function ftpLogin(page) {
  await page.getByTestId('ftp-username').fill(ftpUser);
  await page.getByTestId('ftp-password').fill(ftpPassword);
  await page.getByTestId('ftp-login').click();
  await expect(page.getByTestId('tree-root')).toBeVisible();
}

async function getAppToken(request) {
  const response = await request.post(`${apiBaseUrl}/api/access/login`, {
    data: { username: appUser, password: appPassword }
  });
  expect(response.ok(), `Uygulama test girişi başarısız: ${response.status()}`).toBeTruthy();
  return (await response.json()).token;
}

async function cleanupPath(request, token, remotePath, isFolder) {
  await request.delete(`${apiBaseUrl}/api/ftp/delete`, {
    params: { path: remotePath, isFolder },
    headers: {
      Authorization: `Bearer ${token}`,
      'X-FTP-Server-Id': 'default',
      'X-FTP-Username': ftpUser,
      'X-FTP-Password': ftpPassword
    }
  });
}

test.describe('@rename güvenli dosya gezgini regresyonu', () => {
  test.afterEach(async ({ request }) => {
    const token = await getAppToken(request);
    for (const remotePath of [`/${sourceName}`, `/${renamedName}`]) {
      await cleanupPath(request, token, remotePath, false);
    }
    for (const remotePath of [`/${folderName}`, `/${renamedFolderName}`]) {
      await cleanupPath(request, token, remotePath, true);
    }
  });

  test('hatalı uygulama girişini reddeder', async ({ page }) => {
    await page.goto('/');
    await page.getByTestId('app-username').fill(appUser);
    await page.getByTestId('app-password').fill('__yanlis_sifre__');
    await page.getByTestId('app-login').click();
    await expect(page.getByText(/Giris basarisiz/i)).toBeVisible();
    await expect(page.getByTestId('ftp-login')).toHaveCount(0);
  });

  test('dosya yükler, uzantıyı koruyarak yeniden adlandırır, arar ve siler', async ({ page }) => {
    await appLogin(page);
    await ftpLogin(page);

    await page.getByTestId('upload-file-input').setInputFiles(fixturePath);
    await expect(page.getByText(sourceName, { exact: true })).toBeVisible();
    await page.getByTestId('upload-submit').click();
    await expect(page.getByTestId(`tree-item-/${sourceName}`)).toBeVisible();

    await page.getByTestId(`tree-item-/${sourceName}`).hover();
    await page.getByTestId(`rename-item-/${sourceName}`).click();
    await expect(page.getByTestId('text-input-modal')).toBeVisible();
    await page.getByTestId('text-input-modal-field').fill('__e2e_renamed__');
    await page.getByTestId('text-input-modal-confirm').click();

    await expect(page.getByTestId(`tree-item-/${renamedName}`)).toBeVisible();
    await expect(page.getByTestId(`tree-item-/${sourceName}`)).toHaveCount(0);

    await page.getByTestId('explorer-search').fill('__e2e_renamed__');
    await expect(page.getByTestId(`search-item-/${renamedName}`)).toBeVisible();
    await page.getByTestId('explorer-search').clear();

    await page.getByTestId(`tree-item-/${renamedName}`).hover();
    await page.getByTestId(`rename-item-/${renamedName}`).click();
    await page.getByTestId('text-input-modal-field').fill('__iptal_edilen_ad__');
    await page.getByTestId('text-input-modal-cancel').click();
    await expect(page.getByTestId(`tree-item-/${renamedName}`)).toBeVisible();

    await page.getByTestId(`tree-item-/${renamedName}`).hover();
    await page.getByTestId(`delete-item-/${renamedName}`).click();
    await page.getByTestId('confirm-yes').click();
    await page.getByTestId('confirm-final-yes').click();
    await expect(page.getByTestId(`tree-item-/${renamedName}`)).toHaveCount(0);
  });

  test('klasör oluşturur ve yeniden adlandırır', async ({ page }) => {
    await appLogin(page);
    await ftpLogin(page);

    await page.getByTestId('explorer-create-folder').click();
    await page.getByTestId('text-input-modal-field').fill(folderName);
    await page.getByTestId('text-input-modal-confirm').click();
    await expect(page.getByTestId(`tree-item-/${folderName}`)).toBeVisible();

    await page.getByTestId(`tree-item-/${folderName}`).hover();
    await page.getByTestId(`rename-item-/${folderName}`).click();
    await page.getByTestId('text-input-modal-field').fill(renamedFolderName);
    await page.getByTestId('text-input-modal-confirm').click();
    await expect(page.getByTestId(`tree-item-/${renamedFolderName}`)).toBeVisible();
    await expect(page.getByTestId(`tree-item-/${folderName}`)).toHaveCount(0);
  });
});
