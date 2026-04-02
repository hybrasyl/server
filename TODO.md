# Future work

## NaturalDocs / mono-runtime dependency
NaturalDocs is the only reason `mono-runtime` is in the CI build. It adds significant time to the install step. Worth investigating:
- Does NaturalDocs have a .NET Core / modern .NET version yet?
- Is there a containerized NaturalDocs that could run as a separate step?
- Could docs generation be a separate workflow that doesn't block the main build?
- Is NaturalDocs still the right tool, or has something better emerged?
