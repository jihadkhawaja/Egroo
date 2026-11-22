import { dotnet } from './_framework/dotnet.js';

try {
    const runtime = await dotnet.create();
    await runtime.runMain(runtime.getConfig().mainAssemblyName, []);
} catch (error) {
    console.error(error);
    const status = document.createElement('div');
    status.className = 'avalonia-splash';
    status.setAttribute('role', 'alert');
    status.textContent = 'Egroo could not start. Reload the page to try again.';
    document.body.replaceChildren(status);
}
