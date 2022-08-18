//singleton group for storing all of the groups
if(!isObject($ItemInstance::Group))
{
    $ItemInstance::Group = new ScriptGroup();
}

$ItemInstance::Save = "config/server/ItemInstance/items.txt";

function ItemInstance_Save()
{
    %file = new FileObject();
    %success = %file.OpenForWrite($ItemInstance::Save);

    if(%success)
    {
        %group = $ItemInstance::Group;
        %count = %group.getCount();
        for(%i = 0; %i < %count; %i++)
        {
            %curr = %group.getObject(%i);
            %line = %curr.Serialize();
            %file.writeLine(%line);
        }
    }

    %file.close();
    %file.delete();
}

function ItemInstance_Load()
{
    %file = new FileObject();
    %success = %file.OpenForRead($ItemInstance::Save);

    %currGroup = 0;
    if(%success)
    {
        $ItemInstance::Group.deleteAll();

        while(!%file.isEOF())
        {
            %fields = %file.readLine();
            
            //check if this field is for a group
            if(getWord(%fields,0) $= "GROUP")
            {
                %currGroup = new ScriptGroup(ItemInstanceGroup);
                %currGroup.shapeClass = getWord(%fields,1);
                %currGroup.shapeIdentifier = getWords(%fields,2);
            }
            else if(isObject(%currGroup))
            {
                //not a group add a new instanceitem to the current group
                %item = new ScriptObject(ItemInstance);

                %fieldCount = getFieldCount(%fields);
                for(%i = 0; %i < %fieldCount; %i+=2)
                {
                    %words = getFields(%fields,%i,%i+1);
                    %key = getWord(%words,0);
                    %value = getWords(%words,1);

                    eval("%item." @ %key @ " = %value;");
                }

                %currGroup.add(%item);
            }
        }
    }
    else
    {
        warn("ItemInstance_Load: Failed to open save file.");
    }

    %file.close();
    %file.delete();
}

function ShapeBase::GetItemInstance(%obj,%tool)
{
    %item = "";
    %group = %obj.ItemInstanceGroup;

    if(!isObject(%group))
    {
        warn("ShapeBase::GetItemInstance: ItemInstanceGroup doesn't exist wtf.");
        return;
    }

    if(%tool $= "")
    {
        if(%group.getCount() > 0)
        {
            %item = %group.getObject(0);
        }
        else
        {
            %item = new ScriptObject(ItemInstance);
            %group.add(%item);
        }
    }
    else
    {
        if(isObject(%obj.tool[%tool]))
        {
            %item = %group.getTool(%tool);

            if(!isObject(%item))
            {
                %item = new ScriptObject(ItemInstance){tool = %tool;};
                %group.add(%item);
            }
        }
    }

    if(!isObject(%item))
    {
        warn("ShapeBase::GetItemInstance: No item to instance from.");
        return - 1;
    }

    return %item;
}

function ShapeBase::AddItemInstance(%obj,%itemInstance)
{
    if(!isObject(%itemInstance))
    {
        %itemInstance = new ScriptObject(ItemInstance);
    }
    else if(%itemInstance.getName() !$= "ItemInstance" || %itemInstance.getClassName() !$= "ScriptObject")
    {
        warn("ShapeBase::AddItemInstance: " @ %obj @ " is not a ItemInstance.");
        return;
    }

    %obj.ItemInstanceGroup.add(%ItemInstance);

    return %obj;
}

//player inventory specific
function ItemInstanceGroup::GetTool(%obj,%tool)
{
    %shape = %obj.shape;
    if(%shape.getDataBlock().getClassname() !$= "PlayerData")
    {
        warn("ItemInstanceGroup::GetTool: " @ %shape @ " is not a player.");
        return;
    }

    //search through group and get the tool
    %currItem = "";
    %count = %obj.getCount();
    for(%i = 0; %i < %count; %i++)
    {
        %currItem = %obj.getObject(%i);
        if(%tool == %currItem.tool && %currItem.tool !$= "")
        {
            break;
        }
    }

    if(%i >= %count)
    {
        return -1;
    }

    return %curritem;
}

function ItemInstanceGroup::AddTool(%obj,%tool,%itemInstance)
{
    %shape = %obj.shape;
    if(%shape.getDataBlock().getClassname() !$= "PlayerData")
    {
        warn("ItemInstanceGroup::AddTool: " @ %shape @ " is not a player.");
        return;
    }

    %toolData = %shape.tool[%tool];
    if(!isObject(%toolData))
    {
        warn("ItemInstanceGroup::AddTool: tool " @ %tool @ " is empty.");
        return;
    }
    
    //remove item for this slot
    %count = %obj.getCount();
    for(%i = %count - 1; %i >= 0; %i--)
    {
        %currII = %obj.getObject(%i);
        if(%currII.tool == %tool)
        {
            %currII.delete();
        }
    }

    if(!isObject(%itemInstance))
    {
        %itemInstance = new ScriptObject(ItemInstance);
        %obj.add(%itemInstance);
    }

    %itemInstance.tool = %tool;
    %obj.add(%itemInstance);

    return %itemInstance;
}

function ItemInstanceGroup::Serialize(%obj)
{
    %line = "";
    %shape = %obj.shape;

    if(isObject(%shape))
    {
        %shapeIdentifier = %shape.GetItemInstanceShapeIdentifier();
    }
    else
    {
        %shapeIdentifier = %obj.shapeIdentifier;
    }

    %line = "GROUP" SPC %shapeClass SPC %shapeIdentifier;
    
    //add item instance lines
    %count = %obj.getCount();
    for(%i = 0; %i < %count; %i++)
    {
        %curr = %obj.getObject(0);
        %line = %line NL %curr.Serialize();
    }

    return trim(%line);
}

function ItemInstanceGroup::OnAdd(%obj)
{
    $ItemInstance::Group.add(%obj);
}

function ItemInstance::Set(%item,%key,%value)
{
    %item.value[getSafeVariableName(%key)] = %value;

    return %item;
}

function ItemInstance::Add(%item,%key,%value)
{
    %item.value[getSafeVariableName(%key)] += %value;

    return %item;
}

function ItemInstance::Get(%item,%key,%value)
{
    return %item.value[getSafeVariableName(%key)];
}

function ItemInstance::Serialize(%item)
{
    //loop through all the fields and save the ones that start with value
    %line = "";
    %c = 0;
    while((%field = %item.getTaggedField(%c)) !$= "")
    {
        %line = %line TAB %field;
        %c++;
    }
    return trim(%line);
}

function Item::SetItemInstanceFromThrower(%obj)
{
    %player = findClientByBl_Id(%obj.bl_id).player;
    if(isObject(%player))
    {
        //loop through the group and find ItemInstance with the missing tool
        %item = "";

        %group = %player.itemInstanceGroup;
        if(!isObject(%group))
        {
            warn("Item::SetItemInstanceFromThrower: ItemInstanceGroup doesn't exist wtf.");
            return;
        }

        %count = %group.getCount();
        for(%i = 0; %i < %count; %i++)
        {
            %item = %group.getObject(%i);
            %tool = %item.tool;
            if(!isObject(%player.tool[%tool]))
            {
                %obj.AddItemInstance(%item);
                break;
            }
        }
    }
}

function ShapeBase::GetItemInstanceShapeIdentifier(%shape)
{
    %shapeClass = %shape.getDatablock().getClassName();
    %shapeIdentifier = "";

    %shapeClass = %shape.getDataBlock().getClassname();
    switch$(%shapeClass)
    {
    case "PlayerData":
        if(isObject(%shape.client))
        {
            %shapeIdentifier = %shape.client.getBLID();
        }
    case "ItemData":
        %shapeIdentifier = %shape.getTransform() SPC %shape.getDataBlock().getName();
    }
    return %shapeIdentifier;
}

function ShapeBase::GetItemInstanceGroupFromSave(%obj)
{
    %shapeIdentifier = %obj.GetItemInstanceShapeIdentifier();
    if(%shapeIdentifier $= "")
    {
        return -1;
    }

    %group = $ItemInstance::Group;
    %count = %group.getCount();
    for(%i = 0; %i < %count; %i++)
    {
        %curr = %group.getObject(%i);
        //does this guy already have a shape assigned
        if(isObject(%curr.shape))
        {
            continue;
        }

        if(%curr.shapeIdentifier $= %shapeIdentifier)
        {
            return %curr;
        }
    }

    return -1;
}

//overwriting to fix shapeBaseData callbacks
function Armor::onAdd (%this, %obj)
{
	%obj.setActionThread (root);
	applyDefaultCharacterPrefs (%obj);
	%obj.mountVehicle = 1;
	%obj.setRepairRate (0);

    Parent::onAdd (%this, %obj);
}

function ItemData::onAdd (%this, %obj)
{
	%obj.setSkinName (%this.skinName);
	if (%this.image.doColorShift)
	{
		%obj.setNodeColor ("ALL", %this.image.colorShiftColor);
	}
	%obj.canPickup = 1;

    Parent::onAdd (%this, %obj);
}

function Armor::onRemove (%this, %obj)
{
	if (isObject (%obj.client))
	{
		if (%obj.client.Player == %obj)
		{
			%obj.client.Player = 0;
		}
	}
	if (isObject (%obj.tempBrick))
	{
		%obj.tempBrick.delete ();
	}
	if (isObject (%obj.light))
	{
		%obj.light.delete ();
	}

    Parent::onRemove (%this, %obj);
}

package ItemInstanceOnAddParentFunction
{
    function ShapeBaseData::OnAdd(%db, %obj)
    {

    }
};
if(!isFunction("ShapeBaseData","OnAdd"))
{
    activatePackage(ItemInstanceOnAddParentFunction);
}

package ItemInstanceOnRemoveParentFunction
{
    function ShapeBaseData::OnRemove(%db, %obj)
    {

    }
};
if(!isFunction("ShapeBaseData","OnRemove"))
{
    activatePackage(ItemInstanceOnRemoveParentFunction);
}
      

package ItemInstance
{
    function GameConnection::onClientLeaveGame(%this)
    {
        ItemInstance_Save();
        return Parent::onClientLeaveGame(%this);
    }

    function ShapeBaseData::OnAdd(%db, %obj)
    {
        %r = Parent::OnAdd(%db, %obj);

        %itemInstanceGroup = %obj.GetItemInstanceGroupFromSave();
        if(!isObject(%itemInstanceGroup))
        {
            %itemInstanceGroup = new ScriptGroup(ItemInstanceGroup);
        }

        %obj.itemInstanceGroup = %itemInstanceGroup;
        %obj.itemInstanceGroup.shape = %obj;

        return %r;
    }

    function ShapeBaseData::OnRemove(%db, %obj)
    {
        %group = %obj.itemInstanceGroup;

        if(isObject(%group))
        {
            %group.delete();
        }

        return Parent::OnRemove(%db, %obj);
    }

    function ItemData::onPickup (%this, %obj, %user, %amount)
    {
        //sigh looks like i have to play "find the difference"
        %maxTools = %user.getDatablock().maxTools;
        for(%i = 0; %i < %maxTools; %i++)
        {
            %before[%i] = %user.tool[%i];
        }

        %itemInstance = %obj.GetItemInstance();
        if(isObject(%obj.itemInstanceGroup))
        {
            %obj.itemInstanceGroup.clear();
        }
       
        %r = parent::onPickup(%this, %obj, %user, %amount);

        %group =  %user.itemInstanceGroup;
        if(isObject(%group))
        {
            for(%i = 0; %i < %maxTools; %i++)
            {
                if(%before[%i] != %user.tool[%i])
                {
                    %group.AddTool(%i,%itemInstance);
                    break;
                }
            }
        }
        
        return %r;
    }

    function ItemData::OnAdd(%db, %obj)
    {
        %obj.schedule(0,"SetItemInstanceFromThrower");
        return Parent::OnAdd(%db, %obj);
    }
};
activatePackage("ItemInstance");

if(!$ItemInstance::Group.Loaded)
{
    ItemInstance_Load();
}

$ItemInstance::Group.Loaded = true;
