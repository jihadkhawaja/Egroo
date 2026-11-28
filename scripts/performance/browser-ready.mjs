import { chromium } from 'playwright-core';

const [url] = process.argv.slice(2);
if (!url) {
    throw new Error('Pass the browser container URL.');
}

const browser = await chromium.launch({
    ...(process.env.EGROO_PERF_CHROMIUM
        ? { executablePath: process.env.EGROO_PERF_CHROMIUM }
        : { channel: 'chrome' }),
    args: ['--use-angle=swiftshader', '--enable-unsafe-swiftshader'],
    timeout: 15_000
});
try {
    const page = await browser.newPage();
    page.on('pageerror', error => console.error(error));
    page.on('console', message => {
        if (message.type() === 'error') console.error(message.text());
    });

    const response = await page.goto(url, { waitUntil: 'commit', timeout: 15_000 });
    if (!response?.ok()) {
        throw new Error('The browser container did not serve the application page.');
    }

    await page.locator('#egroo[data-egroo-ready="true"]').waitFor({ state: 'attached', timeout: 60_000 });
    console.log('Egroo browser startup is ready.');
} finally {
    await browser.close();
}
