# Avalonia release acceptance

Use a specific candidate commit for the API, WebAssembly client, and mobile builds. Record its validation and container-smoke runs alongside the device results. If the candidate changes, repeat the affected checks on the new commit.

## Prepare a mobile test environment

1. Run the candidate API on an isolated self-host with a fresh SQLite database or disposable PostgreSQL database. Use a device-reachable HTTPS URL, test-only server secrets, a certificate trusted by both devices, and a proxy that forwards WebSocket upgrades at the `/chathub` endpoint. Set `Api:AllowedOrigins` to the disposable browser origin when using a browser as the second participant. Follow the [self-host configuration](Avalonia-Client.md#container-images) for the same API settings.
2. Prepare two disposable test identities. Create the first account during the registration check, then open a private conversation with the second account. Prepare a small image, a small document, and one device with a clean installation for permission prompts. Use a second device or the candidate WebAssembly client to send messages while the first phone is offline.
3. Record the candidate Git SHA, build version, signer class, test date, device model, OS and WebView versions, connection type, and API path. Record only test fixture names and redacted logs or screenshots. Keep credentials, provisioning data, and device identifiers in the private test environment.

## Build for real devices

Use Android SDK Platform 36, a compatible JDK, and the .NET 10 Android workload. The `android-test-apk` Actions artifact is intended for the hosted emulator and the default packaged API address. For connected acceptance, build a Release APK from the candidate with the disposable API address compiled in. Test updates with the same package ID and signing key.

Use macOS, compatible Xcode and the .NET 10 iOS workload, and an Apple certificate and provisioning profile for an `ios-arm64` build. The profile needs the app ID and the registered test device. The `ios-arm64-simulator-app` Actions artifact uses ad hoc simulator signing and the interpreter. It can support simulator checks. Use the separately provisioned device build to exercise the device Release path. [Apple device setup](https://developer.apple.com/documentation/Xcode/running-your-app-on-simulated-or-physical-devices)

Example commands run from the candidate checkout. Set `EGROO_TEST_API_URL` to the disposable HTTPS API. On macOS, set `IOS_SIGNING_IDENTITY` and `IOS_PROVISIONING_PROFILE` to the installed test signing identity and profile. Find the intended test phone with ADB or Xcode. Set the device and package variables to the matching device and build output before each install.

```sh
: "${EGROO_TEST_API_URL:?Set the disposable HTTPS API URL}"
adb devices -l
dotnet workload install android
dotnet build src/Egroo.Avalonia/Android -c Release \
  -p:AndroidPackageFormats=apk "-p:EgrooApiUrl=$EGROO_TEST_API_URL"
adb -s "$ANDROID_TEST_DEVICE" install "$ANDROID_TEST_APK"

xcrun devicectl list devices
dotnet workload install ios
dotnet build src/Egroo.Avalonia/iOS -c Release -r ios-arm64 \
  "-p:EgrooApiUrl=$EGROO_TEST_API_URL" \
  "-p:CodesignKey=$IOS_SIGNING_IDENTITY" \
  "-p:CodesignProvision=$IOS_PROVISIONING_PROFILE"
xcrun devicectl device install app --device "$IOS_TEST_DEVICE" "$IOS_TEST_APP"
```

Use the signed APK or .app emitted by that build for `ANDROID_TEST_APK` or `IOS_TEST_APP`. Record the package hash or signed build identifier with the source SHA. [Android device installation](https://developer.android.com/studio/run/device) · [.NET iOS signing properties](https://github.com/dotnet/macios/wiki/Project-file-properties)

## Physical device checks

Run each row on Android and iOS. Record **Pass**, **Fail**, **Blocked**, or **N/A** with a short observation. Use N/A for optional functionality outside the candidate and explain it. Link each reproducible failure to a tracked issue before release.

| Check | Expected result |
| --- | --- |
| Fresh install and registration | The app opens on the device, shows the sign-in screen, creates a disposable account over the test API, and signs in again after sign-out. |
| Encrypted text and files | Both sides exchange text, then send, open, and save the image and document. The latest timeline and read receipt stay correct after reopening. Check the download contents against the fixtures. |
| Permission denial and recovery | Deny microphone access when requested and verify understandable feedback. Grant it through the OS settings, return to Egroo, and retry the call. Cancel the platform file picker and successfully select a fixture on retry. Exercise any additional permission the OS requests. |
| Voice | Join a call from the phone, exchange audio in both directions, mute and unmute, leave, and rejoin. Confirm the microphone indicator clears after leaving. |
| Background and reconnect | Background and resume during a conversation. With the phone offline, send two distinct messages from the other participant. Restore connectivity and verify the two messages appear once in order. Repeat after locking and unlocking the phone. |
| Layout and input | Focus username, password and compose fields. Check keyboard insets, the composer, and scrolling while typing and after dismissal. Rotate where supported and confirm controls and timeline remain usable. |
| Nearby, when included | With the API offline, exercise the candidate's Nearby messaging and voice flows on two phones. Check permission denial and recovery on each relevant platform. |
| Upgrade, when using an existing installation | Install the candidate over an earlier test build signed with the same key and package ID. Confirm new messages decrypt and prior local history remains accessible where the local origin is unchanged. |

Attach a result per platform to [#253](https://github.com/jihadkhawaja/Egroo/issues/253) with the candidate SHA, artifact identifiers, device and OS matrix, scenario outcomes, and tracked failures. Keep the hosted simulator and emulator checks as supporting evidence for their respective packages.

## Promotion and GHCR handoff

1. Pin the reviewed avalonia candidate SHA and its successful validation and no-push container smoke. Prepare the promotion PR targeting main with the physical-device evidence and open acceptance items. Use the current protected branch status checks, including Test, Coverage & Report. Obtain explicit owner approval before merging to main.
2. After approval and promotion, confirm the workflow is present on the default branch and that main points at the intended release source. For [#227](https://github.com/jihadkhawaja/Egroo/issues/227), a maintainer manually runs **Avalonia containers** on main. This workflow rebuilds and smoke-tests both images before it pushes. [GitHub manual workflow dispatch](https://docs.github.com/en/actions/how-tos/manage-workflow-runs/manually-run-a-workflow)
3. Verify the published `ghcr.io/jihadkhawaja/egroo-api` and `ghcr.io/jihadkhawaja/egroo-browser` images use the same `sha-<commit>-<run>-<attempt>` tag. The separate read-only verification job resolves both tags, pulls by digest on a fresh runner, checks each image revision, and starts the pair with a disposable API. Record the run URL and the two immutable references from its summary. Verify both digests are also pullable with the intended hoster access.
4. Record the verified pair in #227. Before deploying a pair, preserve the prior image references, API configuration, and database backup for the documented [rollback steps](Avalonia-Client.md#container-images). Publication and production deployment are separate operations.
