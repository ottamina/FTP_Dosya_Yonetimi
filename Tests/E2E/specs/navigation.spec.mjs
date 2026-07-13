import { test, expect } from '@playwright/test';

const appUser = process.env.E2E_APP_USER || 'admin';
const appPassword = process.env.E2E_APP_PASSWORD || 'admin';
const ftpUser = process.env.E2E_FTP_USER || 'ftpadmin';
const ftpPassword = process.env.E2E_FTP_PASSWORD || 'admin123';

async function appLogin(page) {
  await page.goto('/');
  await page.getByTestId('app-username').fill(appUser);
  await page.getByTestId('app-password').fill(appPassword);
  await page.getByTestId('app-login').click();
  await expect(page.getByTestId('nav-explorer')).toBeVisible();
}

test.describe('@smoke uygulama gezinme ve bağlantı kontrolleri', () => {
  test('FTP hata yolunu, parola görünürlüğünü ve başarılı bağlantıyı doğrular', async ({ page }) => {
    await appLogin(page);

    await page.getByTestId('ftp-username').fill(ftpUser);
    await page.getByTestId('ftp-password').fill('__yanlis_ftp_sifresi__');
    await expect(page.getByTestId('ftp-password')).toHaveAttribute('type', 'password');
    await page.getByTestId('ftp-password-toggle').click();
    await expect(page.getByTestId('ftp-password')).toHaveAttribute('type', 'text');
    await page.getByTestId('ftp-login').click();
    await expect(page.getByText(/Giriş doğrulama hatası|bağlantı başarısız/i)).toBeVisible();

    await page.getByTestId('ftp-password').fill(ftpPassword);
    await page.getByTestId('ftp-login').click();
    await expect(page.getByTestId('tree-root')).toBeVisible();

    await page.getByTestId('explorer-search').fill('__kesinlikle_bulunmayan_e2e_oge__');
    await expect(page.getByText('Sonuç bulunamadı.')).toBeVisible();
    await page.getByTestId('explorer-search').clear();
    await Promise.all([
      page.waitForResponse((response) => response.url().includes('/api/ftp/list') && response.ok()),
      page.getByTestId('explorer-refresh').click()
    ]);
    await expect(page.getByTestId('tree-root')).toBeVisible();
  });

  test('log sekmeleri ve yönetim ekranları arasında gezinir', async ({ page }) => {
    await appLogin(page);
    await page.getByTestId('ftp-username').fill(ftpUser);
    await page.getByTestId('ftp-password').fill(ftpPassword);
    await page.getByTestId('ftp-login').click();
    await expect(page.getByTestId('logs-file-tab')).toBeVisible();

    await page.getByTestId('logs-database-tab').click();
    await expect(page.getByRole('heading', { name: 'LiteDB Veritabanı Logları' })).toBeVisible();
    await page.getByTestId('logs-file-tab').click();
    await expect(page.getByRole('heading', { name: 'JSON Dosya Logları' })).toBeVisible();

    await page.getByTestId('nav-servers').click();
    await expect(page.getByRole('heading', { name: /FTP Sunucu Yönetimi/i })).toBeVisible();
    await page.getByTestId('nav-access').click();
    await expect(page.getByText(/Üye|Rol/i).first()).toBeVisible();
    await page.getByTestId('nav-explorer').click();
    await expect(page.getByText('Dosya Yapısı')).toBeVisible();

    await page.getByTestId('app-logout').click();
    await expect(page.getByTestId('app-login')).toBeVisible();
    await expect(page.getByTestId('nav-explorer')).toHaveCount(0);
  });
});
