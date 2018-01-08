"""Information about the script goes here"""

name = "Resurrector"
description = "Death NPC"
author = "Justin Baugh <baughj@hybrasyl.com>"

function OnClick(self, invoker)
  invoker:SystemMessage("It waves its hands in displeasure.")
  invoker:SystemMessage(name .. " resurrects you.")
  invoker:Resurrect()
end
