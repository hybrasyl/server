#!/bin/bash
#
# Best to not ask questions
#

set -e -x

XSDPATH="XSD/Objects"

#OLD='"4\.8\.4084\.0"'
#NEW='"4\.8"'

# This is no longer necessary now that people that don't know how to use tooling are
# no longer working on the project

#for x in $(ls ${XSDPATH}/*.cs); do
#    sed -i -e "s/${OLD}/${NEW}/g;" $x
#done

git checkout ${XSDPATH}/Castable.cs
git checkout ${XSDPATH}/ElementTableSourceElement.cs
git checkout ${XSDPATH}/ElementTableTargetElement.cs
git checkout ${XSDPATH}/EquipmentRestriction.cs
git checkout ${XSDPATH}/SpawnDamage.cs
git checkout ${XSDPATH}/SpawnDefense.cs
#git checkout ${XSDPATH}/MonsterFormulaSet.cs

# Below doesn't seem to be required with xsd2code 6?

#git checkout ${XSDPATH}/StatModifiers.cs
#git checkout ${XSDPATH}/StatModifierFormulas.cs
#git checkout ${XSDPATH}/PlayerFormula.cs
#git checkout ${XSDPATH}/PlayerRegenFormula.cs
#git checkout ${XSDPATH}/StatModifierFormulas.cs


