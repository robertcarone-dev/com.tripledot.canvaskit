# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

## [0.4.7-preview] - 2026-06-11

### Fixed
- Fixed Canvas preview rendering for prefab variants and nested prefab instances that could trigger Unity's prefab-instance reparenting error.
- Fixed `KeyframeInterpolation` curve saves to sanitize keyframe tangents, weights, and weighted modes before writing animation curves.
- Fixed `KeyframeInterpolation` preset and manual curve application to avoid partial saves when any selected curve cannot be safely updated.
- Fixed `KeyframeInterpolation` showing `Mixed` after applying a preset to flat or otherwise indeterminate selected segments.
- Fixed `KeyframeInterpolation` first-key endpoint handling so multi-key out-handle edits update the first key's out tangent and avoid restoring invalid endpoint tangent selections.


## [0.4.6-preview] - 2026-06-10

### Fixed
- Fixed `KeyframeInterpolation` window sometimes editing keyframes instead of segments.


## [0.4.5-preview] - 2026-06-09

### Added
- Added `ImageLattice` modifier component for animating image's with lattice deformation.
- Added `KeyframeInterpolation` window for editing multiple animation keyframe tangents and interpolation modes.

### Fixed
- Fixed build error due to `Reset` on `TextMeshProLayerStack` not being wrapped by `#ifdef UNITY_EDITOR`.


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
