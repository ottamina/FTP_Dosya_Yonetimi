import { test, expect } from '@playwright/test';
import path from 'node:path';

const appUser = process.env.E2E_APP_USER || 'admin';
const appPassword = process.env.E2E_APP_PASSWORD || 'admin';
const ftpUser = process.env.E2E_FTP_USER || 'ftpadmin';
const ftpPassword = process.env.E2E_FTP_PASSWORD || 'admin123';
const apiBaseUrl = process.env.E2E_API_URL || 'http://127.0.0.1:5230';
const fileName = '__e2e_move_source__.txt';
const folderName = '__e2e_move_folder__';
const fixturePath = path.resolve(import.meta.dirname, '..', 'fixtures', fileName);

async function login(page) {
  await page.goto('/');
  await page.getByTestId('app-username').fill(appUser);
  await page.getByTestId('app-password').fill(appPassword);
  await page.getByTestId('app-login').click();
  await page.getByTestId('ftp-username').fill(ftpUser);
  await page.getByTestId('ftp-password').fill(ftpPassword);
  await page.getByTestId('ftp-login').click();
  await expect(page.getByTestId('tree-root')).toBeVisible();
}

async function appToken(request) {
  const response = await request.post(`${apiBaseUrl}/api/access/login`, {
    data: { username: appUser, password: appPassword }
  });
  return (await response.json()).token;
}

async function cleanup(request, token, remotePath, isFolder) {
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

test.describe('@explorer önizleme, indirme ve taşıma', () => {
  test.afterEach(async ({ request }) => {
    const token = await appToken(request);
    await cleanup(request, token, `/${fileName}`, false);
    await cleanup(request, token, `/${folderName}`, true);
  });

  test('metin dosyasını önizler, indirir ve klasöre taşır', async ({ page }) => {
    await login(page);
    await page.getByTestId('upload-file-input').setInputFiles(fixturePath);
    await page.getByTestId('upload-submit').click();
    const fileItem = page.getByTestId(`tree-item-/${fileName}`);
    await expect(fileItem).toBeVisible();

    await fileItem.click();
    await expect(page.getByTestId('preview-panel')).toBeVisible();
    await expect(page.getByTestId('preview-text')).toContainText('önizleme, indirme ve taşıma testi');
    await page.getByTestId('preview-close').click();

    await fileItem.hover();
    const downloadPromise = page.waitForEvent('download');
    await page.getByTestId(`download-item-/${fileName}`).click();
    const download = await downloadPromise;
    expect(download.suggestedFilename()).toBe(fileName);

    await page.getByTestId('explorer-create-folder').click();
    await page.getByTestId('text-input-modal-field').fill(folderName);
    await page.getByTestId('text-input-modal-confirm').click();
    const folderItem = page.getByTestId(`tree-item-/${folderName}`);
    await expect(folderItem).toBeVisible();

    await fileItem.dragTo(folderItem);
    await expect(fileItem).toHaveCount(0);
    await folderItem.click();
    await expect(page.getByTestId(`tree-item-/${folderName}/${fileName}`)).toBeVisible();
  });

  test('klasör oluşturma penceresini iptal eder', async ({ page }) => {
    await login(page);
    await page.getByTestId('explorer-create-folder').click();
    await page.getByTestId('text-input-modal-field').fill(folderName);
    await page.getByTestId('text-input-modal-cancel').click();
    await expect(page.getByTestId(`tree-item-/${folderName}`)).toHaveCount(0);
  });
});
