#!/bin/bash
#
# Best to not ask questions
#

set -e -x

XSDPATH="XSD/Objects"
OLD='"4\.8\.3752\.0"'
NEW='"4\.8"'

for x in $(ls ${XSDPATH}/*.cs); do
    sed -i -e "s/${OLD}/${NEW}/g;" $x
done

git checkout ${XSDPATH}/Castable.cs
git checkout ${XSDPATH}/ElementTableSourceElement.cs
git checkout ${XSDPATH}/ElementTableTargetElement.cs
git checkout ${XSDPATH}/EquipmentRestriction.cs
git checkout ${XSDPATH}/SpawnCastable.cs
git checkout ${XSDPATH}/SpawnCastableDefense.cs
git checkout ${XSDPATH}/SpawnCastableOffense.cs
git checkout ${XSDPATH}/StatModifiers.cs

