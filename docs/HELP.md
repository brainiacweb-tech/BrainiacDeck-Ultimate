# Offline help
## Start
Choose a tool from the left navigation. Ctrl+Shift+Space opens Command Center when registration succeeds. Settings saves light/dark appearance and window behavior. Tray double-click restores the main window. The close button exits after confirmation when enabled.

## Converter
Drop supported files, select a compatible output, choose an existing output folder, then run the queue. Existing files get a numbered alternative name. Email preset uses JPEG quality 75 and max width 1600. A saved preset stores one reusable configuration. Review per-file results and sizes. Cancellation takes effect between codec phases; it cannot interrupt every native codec call. GIF and TIFF inputs export their first frame only. Transparent JPEG output uses white.

Combine images, text, Markdown source and PDFs into a PDF. Text wrapping is basic; Markdown is source text, not rich rendering. PDF-only queues support page reversal and 90-degree rotation. Separate page tools accept selections such as 1,3-5,2, export selected/reordered pages or split all pages. Signed/encrypted/malformed PDFs may fail; originals remain untouched. Compression is not advertised.

## Clipboard Shelf
Manual Add reads the current clipboard once. Monitoring is off until consent is checked and stops when unchecked or the shelf closes. Search, select, pin, reorder, transform text, copy, save TXT, or open text in the teleprompter. Content is temporary and not restored after restart. Exclusion words apply to text; pause before copying secrets.

## Teleprompter
Import TXT/Markdown or paste text. Start/pause scrolls the reading panel. Click the reading panel before Space/Home shortcuts. Adjust speed, font, width, alignment, opacity and mirroring. Scripts save as ordinary text files. Capture exclusion is a Windows request and must be tested with your recorder; it is not a promise of invisibility.

## File toolkit
Select files, calculate hashes, optionally compare an expected hash, inspect metadata or export results. Preview numbered renames before applying; collisions are rejected and the last operation can be undone. Duplicate scans hash then compare bytes and never delete anything. ZIP extraction creates a new folder and rejects traversal, symbolic links, excessive entries and expansion beyond the size limit. Password-protected ZIPs are not supported.

## Workflows
Choose separate input/output folders, an extension filter and Copy/SHA-256/ZIP steps. Each step reads the original selected source set. Dry-run, review paths, then confirm execution. Results go into a fresh run directory with a log. Cancellation leaves partial results for inspection. No folder watcher or scheduler runs in the background.

## Capture
Choose Primary display, All displays, Select region or Foreground window and a countdown. App panels hide while capturing the desktop. For foreground capture, focus the desired window during the countdown. Region selection uses a desktop overlay; Escape cancels. Open/drop an image to edit an existing file. Choose Crop, drag a rectangle and Apply crop; arrow, rectangle, highlight, opaque redaction and text apply on drag. Color, stroke and text size controls appear for relevant tools. View zoom changes only the editor view. Undo/reset retains original pixels in memory; redaction exports opaque black pixels. Choose Original, 720p, 1080p, square or portrait output and fit/padding or fill/center-crop. Preview export before PNG/JPEG/PDF export, clipboard copy or shelf handoff. Upscaling cannot restore missing source detail; social platforms may recompress uploads. OCR is unavailable.

For video, choose the Recording tab, output size and target frame rate, then Start recording and select a new MP4 filename. Use the floating toolbar to pause/resume or Stop and save. Recording is video-only: no microphone or system audio. Windows is asked to exclude the toolbar from capture, but verify this on your device. Recording stops after two hours; actual frame rate depends on capture/encoding load. Edit the last recording or open a local MP4. The editor supports playback, start/end trim in seconds, a crop selected on the first frame, and centered zoom from 1x to 4x. Export creates a separate H.264 MP4 and does not include imported audio. Existing video files are never overwritten. Cancel removes the unfinished export after the current native codec call returns. During recording/export a temporary .partial.mp4 exists beside the selected output; a forced process exit can leave it behind. Protected desktop content may be blank. Editing supports compatible Windows codecs and frames up to 3840 pixels per side; variable presentation dimensions are rejected.

## Workspaces and privacy
Save collections of tools; safe imported configurations do not execute on import. Privacy Center shows/clears activity and removes saved workspace/workflow/preset/favorite configurations. User-exported files remain yours to manage.

## Troubleshooting
Storage errors are displayed without silently resetting data. Check local profile folder permissions. Shortcut conflicts need another combination in Shortcut Studio. Unsupported/corrupt inputs report failure; do not repeatedly retry untrusted huge inputs. For support, export OS/runtime diagnostics from Privacy Center. Store/MSIX installation is not yet available.