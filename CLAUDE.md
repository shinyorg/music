# Shiny.Music — Working Notes

Guidance for maintaining this repo. The library lives in `src/Shiny.Music/`, the published
Claude Code skill in `skills/shiny-music/`, and the public documentation site in a **separate**
repo at `~/Desktop/dev/documentation` (rendered under the Music section of https://shinylib.net).

Shiny.Music is a unified API for the device music library on **Android**, **iOS**, and
**Mac Catalyst** — permissions, metadata querying, filtering, playback, lyrics, album art,
playlists, play counts, and file copy. It does **not** depend on `Microsoft.Maui.Essentials`;
it works in plain .NET for iOS/Android as well as MAUI.

## Repo layout

- `src/Shiny.Music/` — the shipped library. Shared code at the root; platform code under
  `Platforms/Android/` and `Platforms/Apple/` (Apple = iOS + Mac Catalyst, one shared folder).
- `macios/ShinyMusicKit.Binding/` — hand-authored Swift MusicKit binding, packaged separately as
  `Shiny.MusicKit.Binding` and referenced by the Apple target of `Shiny.Music`.
- `src/Shiny.Spotify.Maui/` — cross-platform Spotify App Remote library: one `ISpotifyRemote` interface with
  `Platforms/Android` + `Platforms/Apple` implementations over the bindings, plus the Web API client and
  PKCE auth service. **Not shipped yet** (see below); `IsPackable=false`.
- `sample/MusicSample/` — MAUI sample app. **Not published** to NuGet.
- `bindings/Shiny.Spotify.AppRemote.*` — low-level Spotify App Remote bindings (raw AAR / xcframework), kept as
  separate per-platform projects because they fail to compile when combined into one multi-target project.
  Consumed only by `Shiny.Spotify.Maui`. **Not shipped** (see below).
- `build.slnf` — the CI build/pack filter. It intentionally contains **only** `Shiny.Music` and
  `ShinyMusicKit.Binding`; that is the entire published surface.

Packaging is driven by `Directory.build.props` (`GeneratePackageOnBuild` in Release for all
packable projects). Anything that must not ship carries `<IsPackable>false</IsPackable>`.

## Spotify — NOT documented or released yet

We are **not** shipping or documenting the Spotify integration at this time. Treat `Shiny.Spotify.Maui` and
its bindings as internal, experimental work only:

- **Do not** package it. `src/Shiny.Spotify.Maui`, `bindings/Shiny.Spotify.AppRemote.Android`, and
  `bindings/Shiny.Spotify.AppRemote.iOS` are all `IsPackable=false` and excluded from `build.slnf`. The
  proprietary Spotify SDK binaries are **not** redistributed (fetched via
  `bindings/fetch-spotify-sdks.sh`). Keep those guarantees intact.
- **Do not** mention Spotify in `readme.md`, the docs site, release notes, or the skill.
- **Do not** add a `Shiny.Spotify.Maui` PackageId or `build.slnf` entry without an explicit decision to
  release it.
- The proprietary Spotify SDK binaries (Android `.aar`, iOS `SpotifyiOS.xcframework`) are fetched
  **automatically at build time** — each binding's `.csproj` runs `bindings/fetch-spotify-sdks.sh`
  via an `InitialTargets` hook when its binary is missing. First build of a clean checkout downloads
  them; subsequent builds skip (guarded by an `!Exists(...)` condition). Run the script manually with
  `android`/`ios`/`all` if needed.

If asked to work on Spotify, keep the changes confined to `bindings/` and `sample/` and leave the
published/documented surface untouched.

## Documentation site

The public docs live in a **separate repo**: `~/Desktop/dev/documentation` (Astro / Starlight).

- Feature pages: `src/content/docs/music/*.md(x)` — e.g. `index.mdx` (Getting Started),
  `permissions.md`, `querying.md`, `playback.md`, `lyrics.md`, `album-art.md`, `copying.md`.
- Release notes: `src/content/docs/music/release-notes.mdx`.
- Menu (sidebar): `src/sidebar-topics.mjs` — the **Music** node lives under the **Platform Data**
  topic; add/update entries when you add a feature page.

### Required updates for EVERY fix & feature

A change is not "done" until these are in sync (excluding Spotify, per above):

1. **readme.md** (repo root) — packed into the NuGet package; reflect new/changed behavior.
2. **Skill** (`skills/shiny-music/SKILL.md`) — the agent-facing "how to generate correct code" doc;
   update the trigger keyword list when a new public API is introduced.
3. **Docs site** — update the relevant feature page and add a **release note**.

### Release notes

Notes use the `<RN>` component (`import RN from '/src/components/ReleaseNote.astro'`), with
`type="feature|enhancement|fix|chore"`, an optional `breaking` flag, and an optional
`platform="iOS|Android"` attribute for platform-specific notes. Group under a `## v3`-style version
heading; the newest version section stays at the top. Use a `### <version> - TBD` heading for
unreleased work and promote it to a dated heading (`### 3.1 - July 4, 2026`) when cutting the release.

## Blog posts (only when explicitly requested)

Do **not** write blog posts automatically as part of a fix/feature. Write them **only when the user asks**. When asked to blog a feature, produce **two** posts — first the docs-site version, then adapt it for the personal blog.

### 1. Docs site — `~/Desktop/dev/documentation`

- File: `src/content/docs/blog/YYYY/MM/<slug>.mdx` (current year/month folders; create the month folder if needed).
- Frontmatter:
  ```yaml
  ---
  title: '...'
  description: '...'
  date: YYYY-MM-DD
  authors:
    - allanritchie
  tags:
    - Release        # or Feature, AI, etc.
  ---
  ```
- Body is MDX. Reuse components where relevant, e.g. `import NugetBadge from '/src/components/NugetBadge.astro';` then `<NugetBadge name="Shiny.Music" />`.
- Voice: product/release-note tone — what shipped, breaking changes, code samples, how to use it. **No hero image** on this site.

### 2. Personal blog — `~/Desktop/dev/blog` (adapt the docs post)

- File: `src/content/blog/YYYY/MM/<slug>.mdx` (note: `content/blog`, not `content/docs/blog`).
- Frontmatter (different schema — see `src/content.config.ts`):
  ```yaml
  ---
  title: '...'
  description: '...'
  pubDate: 'Mon DD YYYY'                          # e.g. 'Jul 4 2026'
  heroImage: '../../../../assets/<slug>-hero.svg'
  tags: ['Shiny', '.NET MAUI']
  ---
  ```
- Voice: rework the docs post into a personal, first-person narrative ("Here's something that shouldn't be hard but is…", "So I built…") — story/motivation up front, not a dry changelog.
- **Hero image is required.** Create `src/assets/<slug>-hero.svg`:
  - SVG, `viewBox="0 0 1200 630"`, `width="1200" height="630"`.
  - Match the house style: dark navy/indigo gradient background (`#0f172a` → `#1e1b4b`), cyan/green/violet accent gradients, subtle glow filters, the feature name as the headline. Crib an existing one (e.g. `datasync-hero.svg`, `documentdb-orleans-hero.svg`) as a starting template.
