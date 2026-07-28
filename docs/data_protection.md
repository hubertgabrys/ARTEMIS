# Data Protection

Do not commit clinical data, patient identifiers, credentials, local server secrets, or generated clinical outputs.

Never commit:

- DICOM files.
- `.env`, local config files, passwords, tokens, or ARIA connection details.
- ARTEMIS ESAPI configuration files such as `USZ_ARTEMIS.esapi.json`,
  `USZ_ARTEMIS_Preprocessing.local.config`, or other config files with real
  server paths.
- Generated logs.
