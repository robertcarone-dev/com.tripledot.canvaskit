# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

## [0.4.4-preview] - 2026-06-04

### Fixed
- Fixed TMP stack shadow paint returning zero and not rendering.
- Fixed canvas preview on Unity 6.0 due to API only available in 6.3+.


## [0.4.3-preview] - 2026-06-01

### Added
- Added a TextMeshPro layer inspector warning when shadow spread or blur is clamped by the available font atlas padding or other SDF effects consuming the layer budget.

### Changed
- Removed unused internal editor and preview helper methods.

### Fixed
- Fixed disabled TextMeshPro fill dilate values incorrectly consuming stroke and shadow SDF budget.
- Fixed zero-blur TextMeshPro shadows rendering inconsistently by using a deterministic hard outside-coverage path.
- Fixed TextMeshPro visual padding bounds calculation to include sprite glyphs.


## [0.4.2-preview] - 2026-05-18

### Added
- Added fake bevel lighting support to the text core shader.

### Fixed
- Fixed text shader incorrectly using shader_feature for runtime shader variant selection.


## [0.4.1-preview] - 2026-05-14

### Added
- Added project inspector previews for eligible Canvas prefab assets.


## [0.4.0-preview] - 2026-05-12

### Added
- Initial Canvas Kit package.
