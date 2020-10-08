#!/bin/bash
#

echo -e "Running migrations...\n"

PYTHON=$(which python)

# check a few places
if [[ -z "${PYTHON}" ]]; then
   if [[ -e /usr/bin/python3 ]]; then
       PYTHON=/usr/bin/python3
   elif [[ -e /usr/local/bin/python3 ]]; then
       PYTHON=/usr/local/bin/python3
   fi
fi

echo "Checking ${PYTHON}"

${PYTHON} -c 'import sys; exit(1) if sys.version_info.major < 3 else exit(0)'

if [[ $? == 1 ]]; then
    echo "Python 3 is required tom run migrations."
    exit 1
fi

for x in *.py; do
    echo -e "Running $x\n"
    ${PYTHON} ${x}
done

