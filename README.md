# ARTEMIS

ARTEMIS: Adaptive Radiation Therapy Enhanced by Magnetic Resonance Imaging Systems.

This repository is a monorepo with two independent clinical software subprojects:

- `preprocessing_script/`: Python preprocessing pipeline and GUI, built as a standalone executable with PyInstaller.
- `esapi_script/`: Eclipse Scripting API (ESAPI) project.

The Python pipeline also contains an Eclipse launcher script in `preprocessing_script/eclipse_launcher/`.

## Documentation

- Python development: see `preprocessing_script/README.md`.
- Python executable deployment: see `docs/preprocessing_python_script.md`.
- ESAPI development: see `esapi_script/README.md`.
- ESAPI plan-copy isocenter behavior: see `docs/esapi_script.md`.
- Data handling rules: see `docs/data_protection.md`.
