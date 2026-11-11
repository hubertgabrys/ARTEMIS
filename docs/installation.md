# Installation

## ARTEMIS Preprocessing

Install Python 3.12.

```bash
cd preprocessing
python3.12 -m venv .venv
. .venv/bin/activate
python -m pip install -c constraints-dev.txt -e '.[dev]'
python -m pytest
```

Create a local `.env` file from `configs/env.example`. Do not commit `.env`.

## Visual Studio / ESAPI

Install Visual Studio with .NET Framework 4.8 targeting support.

Place the Varian ESAPI 19.0 DLLs under:

```text
eclipse_script/ESAPI-19.0/
```

Open `eclipse_script/USZ_ARTEMIS.sln` and restore NuGet packages before building.

Copy `eclipse_script/USZ_ARTEMIS/Configuration/USZ_ARTEMIS.AppPaths.example.json`
to `USZ_ARTEMIS.esapi.json` and replace the placeholder paths for the clinical
deployment. The JSON may sit beside the deployed ESAPI assembly or be
referenced with the `USZ_ARTEMIS_APP_PATHS` environment variable. When
`USZ_ARTEMIS.esapi.json` exists under the source `Configuration/` directory,
the Visual Studio build copies it beside the output DLL.

Set `CouchConfiguration` to the commissioned configuration name when the couch
model has multiple configurations. Leave it empty only for a model with a
single configuration. See the [ESAPI migration notes](../eclipse_script/README.md)
for the required Eclipse 19.0 validation before clinical deployment.
