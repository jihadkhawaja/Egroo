import { dotnet } from './_framework/dotnet.js';

async function loadApiUrl() {
    const response = await fetch(new URL('./egroo-config.json', import.meta.url), { cache: 'no-cache' });
    if (response.status === 404)
        return null;
    if (!response.ok)
        throw new Error(`Failed to load Egroo browser configuration (HTTP ${response.status}).`);

    const { apiUrl } = await response.json();
    if (apiUrl != null && typeof apiUrl !== 'string')
        throw new Error('Egroo browser configuration apiUrl must be a string.');
    return apiUrl?.trim() || null;
}

try {
    const [runtime, apiUrl] = await Promise.all([dotnet.create(), loadApiUrl()]);
    const args = apiUrl ? [apiUrl] : [];
    await runtime.runMain(runtime.getConfig().mainAssemblyName, args);
} catch (error) {
    console.error(error);
    const status = document.createElement('div');
    status.className = 'avalonia-splash';
    status.setAttribute('role', 'alert');
    status.textContent = 'Egroo could not start. Reload the page to try again.';
    document.body.replaceChildren(status);
}
