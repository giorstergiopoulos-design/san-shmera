# Security model — Chronikologio

## Trust model
- **Runs as the current user**, never elevated. The installer asks for administrator rights only when it has to write into the Opsis folder under Program Files; otherwise the plugin goes to `%AppData%`.
- **Plugin inside Opsis**: it lives in the plugin folder of Opsis, which any process of the same user may be able to write (when installed in `%AppData%`). The plugin folder has the same trust level as the user account. Loading and verification are done by the Opsis host.
- **No injection, no changes to Windows**: the app reads its own data files and writes only its settings and `plugin.log` in `%AppData%\Chronikologio`.

## Network
- The stand-alone app asks GitHub once a day (`api.github.com/repos/giorstergiopoulos-design/san-shmera/releases/latest`, ETag, 15 s timeout, no personal data) whether a newer version exists; it can be turned off in the Help menu. The Opsis plugin makes **no** network calls: inside Opsis the update button of Opsis does the updating.
- An update is downloaded only from the official releases of the repository (HTTPS, GitHub hosts only), saved to `%LocalAppData%\Chronikologio\update`, and **runs only if its SHA-256 equals the one published in the `.sha256` file of the release** (and its digital signature, if it has one, is valid). Otherwise it is deleted and nothing runs. The installer is started by the shell, with no hidden PowerShell. The result is written to `last-update.log` in that folder.
- Data and images are installed with the app.
- The tools in `tools/` (used only by the maintainer to prepare data) use an honest client name, respect robots.txt and wait between requests. They are not part of the installed app.

## Data
- No secrets, no accounts and no personal data are stored. `plugin.log` holds only technical messages.

## Open points
- The installer and executables are **not code-signed**: Windows SmartScreen will warn; verify the published SHA-256 in the `.sha256` file of each release.
- The image decoder (SixLabors.ImageSharp 3.1.12) has published advisories. The app uses it only for the images it ships itself, never for files from the network or the user.

## Reporting
Open a private security advisory on GitHub or an issue without sensitive details.
