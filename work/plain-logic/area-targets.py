from pathlib import Path
p=Path(r'C:\Users\Gray\Documents\My Games\ArmaReforgerWorkbench\addons\Lobby2')
def edit(name,fn):
 f=p/name;f.write_text(fn(f.read_text()),newline='\n')
def condition(s):
 s=s.replace('\tstring m_sAreaTarget;', '\tint m_iAreaTargets;\n\tref array<string> m_aAreaNames;\n\tint m_iAreaWho;\n\tref array<string> m_aAreaFactions;\n\tint m_iAreaComparison;\n\tint m_iAreaRequired;')
 s=s.replace('return !m_sAreaTarget.IsEmpty() && !m_sAreaName.IsEmpty();','return ValidateAreaTargets();')
 s=s.replace('areas.GetObservation(this).Passes(m_bAreaOutside)', 'areas.GetObservation(this).Passes(m_bAreaOutside, m_iAreaComparison, m_iAreaRequired)')
 at='\t//------------------------------------------------------------------------------------------------\n\tbool UsesArea'
 methods='''\t//------------------------------------------------------------------------------------------------
\tprotected bool ValidateAreaTargets()
\t{
\t\tif (m_sAreaName.IsEmpty() || m_iAreaTargets < 0 || m_iAreaTargets > 1 || m_iAreaComparison < 0 || m_iAreaComparison > 3 || m_iAreaRequired < 0 || m_iAreaRequired > 4096)
\t\t\treturn false;
\t\tarray<string> keys = m_aAreaNames;
\t\tif (m_iAreaTargets == 1)
\t\t{
\t\t\tif (m_iAreaWho < 0 || m_iAreaWho > 2)
\t\t\t\treturn false;
\t\t\tkeys = m_aAreaFactions;
\t\t}
\t\telse if (!keys || keys.IsEmpty())
\t\t\treturn false;
\t\tif (keys)
\t\t{
\t\t\tif (keys.Count() > 256)
\t\t\t\treturn false;
\t\t\tforeach (string key : keys)
\t\t\t{
\t\t\t\tif (key.IsEmpty())
\t\t\t\t\treturn false;
\t\t\t}
\t\t}
\t\treturn true;
\t}

'''
 return s.replace(at,methods+at)
edit('scripts/Game/MissionFramework/GC_RuleCondition.c',condition)
def engine(s):
 s=s.replace('BaseWorld damageWorld = null)', 'BaseWorld damageWorld = null, GC_LobbyManager areaManager = null)')
 s=s.replace('RegisterAreaConditions(damageWorld);', 'RegisterAreaConditions(damageWorld, areaManager);')
 s=s.replace('RegisterAreaConditions(BaseWorld world)', 'RegisterAreaConditions(BaseWorld world, GC_LobbyManager manager)')
 s=s.replace('new GC_MissionAreaTracker(world)', 'new GC_MissionAreaTracker(world, manager)')
 s=s.replace('256 unique target/area pairs', '256 unique target-selection/area combinations')
 return s
edit('scripts/Game/MissionFramework/GC_MissionRuleEngine.c',engine)
edit('scripts/Game/GC_LobbyManager.c',lambda s:s.replace('eventNames, casualties, GetGame().GetWorld());', 'eventNames, casualties, GetGame().GetWorld(), this);'))
f=p/'scripts/Game/MissionFramework/GC_MissionAreaTracker.c'
s=f.read_text().split('//! One result for each unique target/area pair')[0]
s=s.replace('\tprotected bool m_bReportedInvalid;', '\tprotected bool m_bReportedInvalid;\n\tprotected ref map<IEntity, bool> m_mMembership = new map<IEntity, bool>();')
s=s.replace('\t\tIEntity entity = m_Reference.m_Entity;', '\t\tm_mMembership.Clear();\n\t\tIEntity entity = m_Reference.m_Entity;')
at='\t//------------------------------------------------------------------------------------------------\n\tbool Contains(vector position)'
s=s.replace(at,'''\t//------------------------------------------------------------------------------------------------
\tbool ContainsEntity(IEntity entity, vector position)
\t{
\t\tbool inside;
\t\tif (m_mMembership.Find(entity, inside))
\t\t\treturn inside;
\t\tinside = Contains(position);
\t\tm_mMembership.Set(entity, inside);
\t\treturn inside;
\t}

'''+at)
s+='''//! Shared counts for a target selection and area; comparison and side stay on the condition.
class GC_MissionAreaObservation
{
\tGC_RuleCondition m_Definition;
\tref array<ref GC_AreaEntityReference> m_aTargets = {};
\tref GC_MissionAreaGeometry m_Area;
\tbool m_bChanged;
\tprotected bool m_bAvailable;
\tprotected int m_iInside;
\tprotected int m_iTotal;
\tprotected bool m_bSampled;

\t//------------------------------------------------------------------------------------------------
\tbool Matches(GC_RuleCondition other)
\t{
\t\tif (m_Definition.m_sAreaName != other.m_sAreaName || m_Definition.m_iAreaTargets != other.m_iAreaTargets)
\t\t\treturn false;
\t\tarray<string> first = m_Definition.m_aAreaNames;
\t\tarray<string> second = other.m_aAreaNames;
\t\tif (other.m_iAreaTargets == 1)
\t\t{
\t\t\tif (m_Definition.m_iAreaWho != other.m_iAreaWho)
\t\t\t\treturn false;
\t\t\tfirst = m_Definition.m_aAreaFactions;
\t\t\tsecond = other.m_aAreaFactions;
\t\t}
\t\tint firstCount;
\t\tint secondCount;
\t\tif (first)
\t\t\tfirstCount = first.Count();
\t\tif (second)
\t\t\tsecondCount = second.Count();
\t\tif (firstCount != secondCount)
\t\t\treturn false;
\t\tif (first)
\t\t{
\t\t\tforeach (string value : first)
\t\t\t{
\t\t\t\tif (!second || !second.Contains(value))
\t\t\t\t\treturn false;
\t\t\t}
\t\t}
\t\treturn true;
\t}

\t//------------------------------------------------------------------------------------------------
\tvoid Sample(GC_AreaPopulation population)
\t{
\t\tbool available = m_Area.IsAvailable();
\t\tint total;
\t\tint inside;
\t\tif (m_Definition.m_iAreaTargets == 0)
\t\t{
\t\t\tforeach (GC_AreaEntityReference target : m_aTargets)
\t\t\t{
\t\t\t\tif (!target.m_Entity)
\t\t\t\t{
\t\t\t\t\tavailable = false;
\t\t\t\t\tcontinue;
\t\t\t\t}
\t\t\t\ttotal++;
\t\t\t\tif (available && m_Area.ContainsEntity(target.m_Entity, target.m_vPosition))
\t\t\t\t\tinside++;
\t\t\t}
\t\t}
\t\telse if (!population || !population.IsAvailable())
\t\t\tavailable = false;
\t\telse if (available)
\t\t{
\t\t\tforeach (GC_AreaCharacter character : population.m_aCharacters)
\t\t\t{
\t\t\t\tif ((m_Definition.m_iAreaWho == 0 && !character.m_bPlayer) || (m_Definition.m_iAreaWho == 1 && character.m_bPlayer))
\t\t\t\t\tcontinue;
\t\t\t\tarray<string> factions = m_Definition.m_aAreaFactions;
\t\t\t\tif (factions && !factions.IsEmpty() && !factions.Contains(character.m_sFaction))
\t\t\t\t\tcontinue;
\t\t\t\ttotal++;
\t\t\t\tif (m_Area.ContainsEntity(character.m_Entity, character.m_vPosition))
\t\t\t\t\tinside++;
\t\t\t}
\t\t}
\t\tm_bChanged = !m_bSampled || available != m_bAvailable || inside != m_iInside || total != m_iTotal;
\t\tm_bSampled = true;
\t\tm_bAvailable = available;
\t\tm_iInside = inside;
\t\tm_iTotal = total;
\t}

\t//------------------------------------------------------------------------------------------------
\tbool IsAvailable()
\t{
\t\tif (!m_bAvailable || !m_Area.IsAvailable())
\t\t\treturn false;
\t\tforeach (GC_AreaEntityReference target : m_aTargets)
\t\t{
\t\t\tif (!target.m_Entity)
\t\t\t\treturn false;
\t\t}
\t\treturn true;
\t}

\t//------------------------------------------------------------------------------------------------
\tbool Passes(bool outside, int comparison, int required)
\t{
\t\tif (!IsAvailable())
\t\t\treturn false;
\t\tint count = m_iInside;
\t\tif (outside)
\t\t\tcount = m_iTotal - m_iInside;
\t\tif (comparison == 0)
\t\t\treturn count > required;
\t\tif (comparison == 1)
\t\t\treturn count < required;
\t\tif (comparison == 2)
\t\t\treturn count == required;
\t\treturn comparison == 3 && count != required;
\t}
}

//! One server-owned sampler. Native registries supply populations; no spawn overrides or spatial queries.
class GC_MissionAreaTracker
{
\tprotected BaseWorld m_World;
\tprotected GC_LobbyManager m_Manager;
\tprotected ref GC_AreaPopulation m_Population;
\tprotected bool m_bIncludeAI;
\tprotected ref map<string, ref GC_AreaEntityReference> m_mEntities = new map<string, ref GC_AreaEntityReference>();
\tprotected ref map<string, ref GC_MissionAreaGeometry> m_mAreas = new map<string, ref GC_MissionAreaGeometry>();
\tprotected ref array<ref GC_MissionAreaObservation> m_aPairs = {};
\tprotected ref map<GC_RuleCondition, ref GC_MissionAreaObservation> m_mChecks = new map<GC_RuleCondition, ref GC_MissionAreaObservation>();

\t//------------------------------------------------------------------------------------------------
\tvoid GC_MissionAreaTracker(BaseWorld world, GC_LobbyManager manager = null)
\t{
\t\tm_World = world;
\t\tm_Manager = manager;
\t}

\t//------------------------------------------------------------------------------------------------
\tprotected GC_AreaEntityReference GetReference(string name)
\t{
\t\tGC_AreaEntityReference resolved;
\t\tif (m_mEntities.Find(name, resolved))
\t\t\treturn resolved;
\t\tresolved = new GC_AreaEntityReference();
\t\tresolved.m_sName = name;
\t\tm_mEntities.Set(name, resolved);
\t\treturn resolved;
\t}

\t//------------------------------------------------------------------------------------------------
\tbool Register(GC_RuleCondition check)
\t{
\t\tforeach (GC_MissionAreaObservation pair : m_aPairs)
\t\t{
\t\t\tif (pair.Matches(check))
\t\t\t{
\t\t\t\tm_mChecks.Set(check, pair);
\t\t\t\treturn true;
\t\t\t}
\t\t}
\t\tif (m_aPairs.Count() >= 256)
\t\t\treturn false;
\t\tGC_MissionAreaGeometry area;
\t\tif (!m_mAreas.Find(check.m_sAreaName, area))
\t\t{
\t\t\tarea = new GC_MissionAreaGeometry();
\t\t\tarea.m_Reference = GetReference(check.m_sAreaName);
\t\t\tm_mAreas.Set(check.m_sAreaName, area);
\t\t}
\t\tGC_MissionAreaObservation observation = new GC_MissionAreaObservation();
\t\tobservation.m_Definition = check;
\t\tif (check.m_iAreaTargets == 0)
\t\t{
\t\t\tforeach (string name : check.m_aAreaNames)
\t\t\t{
\t\t\t\tGC_AreaEntityReference target = GetReference(name);
\t\t\t\tif (!observation.m_aTargets.Contains(target))
\t\t\t\t\tobservation.m_aTargets.Insert(target);
\t\t\t}
\t\t}
\t\telse
\t\t{
\t\t\tif (!m_Population)
\t\t\t\tm_Population = new GC_AreaPopulation();
\t\t\tif (check.m_iAreaWho != 0)
\t\t\t\tm_bIncludeAI = true;
\t\t}
\t\tobservation.m_Area = area;
\t\tm_aPairs.Insert(observation);
\t\tm_mChecks.Set(check, observation);
\t\treturn true;
\t}

\t//------------------------------------------------------------------------------------------------
\tGC_MissionAreaObservation GetObservation(GC_RuleCondition check)
\t{
\t\treturn m_mChecks.Get(check);
\t}

\t//------------------------------------------------------------------------------------------------
\tvoid Sample(array<GC_RuleCondition> changed, bool initial = false)
\t{
\t\tforeach (string name, GC_AreaEntityReference resolved : m_mEntities)
\t\t\tresolved.Sample(m_World);
\t\tif (m_Population)
\t\t\tm_Population.Sample(m_Manager, m_bIncludeAI);
\t\tforeach (string areaName, GC_MissionAreaGeometry area : m_mAreas)
\t\t\tarea.Sample();
\t\tforeach (GC_MissionAreaObservation pair : m_aPairs)
\t\t\tpair.Sample(m_Population);
\t\tforeach (GC_RuleCondition check, GC_MissionAreaObservation observation : m_mChecks)
\t\t{
\t\t\tif (initial || observation.m_bChanged)
\t\t\t\tchanged.Insert(check);
\t\t}
\t}
}
'''
f.write_text(s,newline='\n')
