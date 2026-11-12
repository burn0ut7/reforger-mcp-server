//! Lobby2-owned scenario parameter authoring and runtime snapshot types.
//! Definitions are authored in the game-mode prefab. Values are created and
//! changed only by the Lobby2 authority.

enum GC_ELobbyParameterType
{
	BOOL = 0,
	INTEGER,
	FLOAT,
	STRING,
	SELECTION
}

//! Explicit gameplay consumers. NONE remains a mission-authored briefing value.
enum GC_ELobbyParameterEffect
{
	NONE = 0,
	RESPAWN_BUDGET,
	RESPAWN_INTERVAL
}

enum GC_ELobbyIntegerPresentation
{
	EDIT_BOX = 0,
	SPIN_BOX,
	SLIDER
}

enum GC_ELobbyFloatPresentation
{
	EDIT_BOX = 0,
	SLIDER
}

[BaseContainerProps()]
class GC_LobbyParameterOption
{
	[Attribute("OPTION", UIWidgets.EditBox, "Player-facing option label")]
	string m_sLabel;

	[Attribute("option", UIWidgets.EditBox, "Stable option key")]
	string m_sKey;

	[Attribute("", UIWidgets.EditBox, "Opaque future script payload")]
	string m_sPayload;

	string GetLabel() { return m_sLabel; }
	string GetKey() { return m_sKey; }
	string GetPayload() { return m_sPayload; }
}

[BaseContainerProps()]
class GC_LobbyParameterDefinition
{
	static const int MAX_KEY_LENGTH = 64;
	static const int MAX_LABEL_LENGTH = 128;
	static const int MAX_DESCRIPTION_LENGTH = 512;
	static const int MAX_FACTION_KEYS = 16;

	[Attribute("PARAMETER", UIWidgets.EditBox, "Player-facing parameter label")]
	protected string m_sLabel;

	[Attribute("", UIWidgets.EditBoxMultiline, "Player-facing parameter description")]
	protected string m_sDescription;

	[Attribute("0", UIWidgets.ComboBox, "Gameplay consumer; non-NONE requires a nonnegative integer", enumType: GC_ELobbyParameterEffect)]
	protected GC_ELobbyParameterEffect m_eEffect;

	[Attribute("", UIWidgets.EditBox, "Required authoritative owner faction key")]
	protected string m_sOwnerFactionKey;

	[Attribute("", UIWidgets.EditBox, "Additional base-game faction keys allowed to view this parameter. Empty is owner-only")]
	protected ref array<string> m_aVisibleFactionKeys;

	string GetLabel() { return m_sLabel; }
	string GetDescription() { return m_sDescription; }
	GC_ELobbyParameterEffect GetEffect() { return m_eEffect; }
	string GetOwnerFactionKey() { return m_sOwnerFactionKey; }

	array<string> GetVisibleFactionKeys()
	{
		if (!m_aVisibleFactionKeys)
			m_aVisibleFactionKeys = {};

		return m_aVisibleFactionKeys;
	}

	bool IsVisibleToFaction(Faction faction)
	{
		if (!faction)
			return false;

		string factionKey = faction.GetFactionKey();
		if (factionKey == m_sOwnerFactionKey)
			return true;

		if (!m_aVisibleFactionKeys || m_aVisibleFactionKeys.IsEmpty())
			return false;

		foreach (string allowedFactionKey : m_aVisibleFactionKeys)
		{
			if (!allowedFactionKey.IsEmpty() && allowedFactionKey == factionKey)
				return true;
		}

		return false;
	}
}

[BaseContainerProps()]
class GC_LobbyBoolParameterDefinition : GC_LobbyParameterDefinition
{
	[Attribute("0", UIWidgets.CheckBox, "Default boolean value")]
	protected bool m_bDefaultValue;

	bool GetDefaultValue() { return m_bDefaultValue; }
}

[BaseContainerProps()]
class GC_LobbyIntegerParameterDefinition : GC_LobbyParameterDefinition
{
	[Attribute("0", UIWidgets.EditBox, "Default integer value")]
	protected int m_iDefaultValue;

	[Attribute("0", UIWidgets.EditBox, "Minimum integer value")]
	protected int m_iMinimum;

	[Attribute("100", UIWidgets.EditBox, "Maximum integer value")]
	protected int m_iMaximum;

	[Attribute("1", UIWidgets.EditBox, "Integer step")]
	protected int m_iStep;

	[Attribute("0", UIWidgets.ComboBox, "Integer control presentation", enumType: GC_ELobbyIntegerPresentation)]
	protected GC_ELobbyIntegerPresentation m_ePresentation;

	int GetDefaultValue() { return m_iDefaultValue; }
	int GetMinimum() { return m_iMinimum; }
	int GetMaximum() { return m_iMaximum; }
	int GetStep() { return m_iStep; }
	GC_ELobbyIntegerPresentation GetPresentation() { return m_ePresentation; }
}

[BaseContainerProps()]
class GC_LobbyFloatParameterDefinition : GC_LobbyParameterDefinition
{
	[Attribute("0", UIWidgets.EditBox, "Default floating-point value")]
	protected float m_fDefaultValue;

	[Attribute("0", UIWidgets.EditBox, "Minimum floating-point value")]
	protected float m_fMinimum;

	[Attribute("1", UIWidgets.EditBox, "Maximum floating-point value")]
	protected float m_fMaximum;

	[Attribute("0.1", UIWidgets.EditBox, "Floating-point step")]
	protected float m_fStep;

	[Attribute("2", UIWidgets.SpinBox, "Displayed decimal places")]
	protected int m_iPrecision;

	[Attribute("0", UIWidgets.ComboBox, "Floating-point control presentation", enumType: GC_ELobbyFloatPresentation)]
	protected GC_ELobbyFloatPresentation m_ePresentation;

	float GetDefaultValue() { return m_fDefaultValue; }
	float GetMinimum() { return m_fMinimum; }
	float GetMaximum() { return m_fMaximum; }
	float GetStep() { return m_fStep; }
	int GetPrecision() { return m_iPrecision; }
	GC_ELobbyFloatPresentation GetPresentation() { return m_ePresentation; }
}

[BaseContainerProps()]
class GC_LobbyStringParameterDefinition : GC_LobbyParameterDefinition
{
	[Attribute("", UIWidgets.EditBox, "Default string value")]
	protected string m_sDefaultValue;

	[Attribute("256", UIWidgets.SpinBox, "Maximum string length")]
	protected int m_iMaximumLength;

	string GetDefaultValue() { return m_sDefaultValue; }
	int GetMaximumLength() { return m_iMaximumLength; }
}

[BaseContainerProps()]
class GC_LobbySelectionParameterDefinition : GC_LobbyParameterDefinition
{
	[Attribute("", UIWidgets.EditBox, "Default option key")]
	protected string m_sDefaultKey;

	[Attribute("", UIWidgets.Object, "Available parameter options")]
	protected ref array<ref GC_LobbyParameterOption> m_aOptions;

	string GetDefaultKey() { return m_sDefaultKey; }

	array<ref GC_LobbyParameterOption> GetOptions()
	{
		if (!m_aOptions)
			m_aOptions = {};

		return m_aOptions;
	}
}

//! Typed runtime value. Selection values carry only a stable option key; the
//! authored option payload never crosses the network and is resolved later.
class GC_LobbyParameterValue
{
	protected int m_iDefinitionIndex;
	protected GC_ELobbyParameterType m_eType;
	protected bool m_bBoolValue;
	protected int m_iIntValue;
	protected float m_fFloatValue;
	protected string m_sStringValue;
	protected string m_sSelectionKey;

	int GetDefinitionIndex() { return m_iDefinitionIndex; }
	GC_ELobbyParameterType GetType() { return m_eType; }
	bool GetBoolValue() { return m_bBoolValue; }
	int GetIntValue() { return m_iIntValue; }
	float GetFloatValue() { return m_fFloatValue; }
	string GetStringValue() { return m_sStringValue; }
	string GetSelectionKey() { return m_sSelectionKey; }

	void SetBool(int definitionIndex, bool value)
	{
		m_iDefinitionIndex = definitionIndex;
		m_eType = GC_ELobbyParameterType.BOOL;
		m_bBoolValue = value;
	}

	void SetInteger(int definitionIndex, int value)
	{
		m_iDefinitionIndex = definitionIndex;
		m_eType = GC_ELobbyParameterType.INTEGER;
		m_iIntValue = value;
	}

	void SetFloat(int definitionIndex, float value)
	{
		m_iDefinitionIndex = definitionIndex;
		m_eType = GC_ELobbyParameterType.FLOAT;
		m_fFloatValue = value;
	}

	void SetString(int definitionIndex, string value)
	{
		m_iDefinitionIndex = definitionIndex;
		m_eType = GC_ELobbyParameterType.STRING;
		m_sStringValue = value;
	}

	void SetSelection(int definitionIndex, string key)
	{
		m_iDefinitionIndex = definitionIndex;
		m_eType = GC_ELobbyParameterType.SELECTION;
		m_sSelectionKey = key;
	}

	void CopyFrom(GC_LobbyParameterValue source)
	{
		if (!source)
			return;

		m_iDefinitionIndex = source.m_iDefinitionIndex;
		m_eType = source.m_eType;
		m_bBoolValue = source.m_bBoolValue;
		m_iIntValue = source.m_iIntValue;
		m_fFloatValue = source.m_fFloatValue;
		m_sStringValue = source.m_sStringValue;
		m_sSelectionKey = source.m_sSelectionKey;
	}

	string GetNormalizedValue()
	{
		switch (m_eType)
		{
			case GC_ELobbyParameterType.BOOL: return m_bBoolValue.ToString();
			case GC_ELobbyParameterType.INTEGER: return m_iIntValue.ToString();
			case GC_ELobbyParameterType.FLOAT: return m_fFloatValue.ToString(-1, 3);
			case GC_ELobbyParameterType.STRING: return m_sStringValue;
			case GC_ELobbyParameterType.SELECTION: return m_sSelectionKey;
		}

		return string.Empty;
	}

	bool RplSave(ScriptBitWriter writer)
	{
		writer.WriteInt(m_iDefinitionIndex);
		writer.WriteIntRange(m_eType, GC_ELobbyParameterType.BOOL, GC_ELobbyParameterType.SELECTION);
		writer.WriteBool(m_bBoolValue);
		writer.WriteInt(m_iIntValue);
		writer.WriteFloat(m_fFloatValue);
		writer.WriteString(m_sStringValue);
		writer.WriteString(m_sSelectionKey);
		return true;
	}

	bool RplLoad(ScriptBitReader reader)
	{
		if (!reader.ReadInt(m_iDefinitionIndex)
			|| !reader.ReadIntRange(m_eType, GC_ELobbyParameterType.BOOL, GC_ELobbyParameterType.SELECTION)
			|| !reader.ReadBool(m_bBoolValue)
			|| !reader.ReadInt(m_iIntValue)
			|| !reader.ReadFloat(m_fFloatValue)
			|| !reader.ReadString(m_sStringValue)
			|| !reader.ReadString(m_sSelectionKey))
		{
			Print("GC | JIP load parameter value failed: decode", level: LogLevel.ERROR);
			return false;
		}
		if (m_iDefinitionIndex < 0 || m_iDefinitionIndex >= GC_LobbyParameterCatalog.MAX_PARAMETERS
			|| m_sStringValue.Length() > GC_LobbyParameterCatalog.MAX_STRING_VALUE_LENGTH
			|| m_sSelectionKey.Length() > GC_LobbyParameterDefinition.MAX_KEY_LENGTH)
		{
			PrintFormat("GC | JIP load parameter value failed: index=%1 type=%2 stringLength=%3 selectionLength=%4", m_iDefinitionIndex,
				m_eType, m_sStringValue.Length(), m_sSelectionKey.Length(), level: LogLevel.ERROR);
			return false;
		}

		return true;
	}

	static bool Extract(GC_LobbyParameterValue instance, ScriptCtx ctx, SSnapSerializerBase snapshot)
	{
		snapshot.SerializeInt(instance.m_iDefinitionIndex);
		snapshot.SerializeInt(instance.m_eType);
		snapshot.SerializeBool(instance.m_bBoolValue);
		snapshot.SerializeInt(instance.m_iIntValue);
		snapshot.SerializeFloat(instance.m_fFloatValue);
		snapshot.SerializeString(instance.m_sStringValue);
		snapshot.SerializeString(instance.m_sSelectionKey);
		return true;
	}

	static bool Inject(SSnapSerializerBase snapshot, ScriptCtx ctx, GC_LobbyParameterValue instance)
	{
		snapshot.SerializeInt(instance.m_iDefinitionIndex);
		int type;
		snapshot.SerializeInt(type);
		instance.m_eType = type;
		snapshot.SerializeBool(instance.m_bBoolValue);
		snapshot.SerializeInt(instance.m_iIntValue);
		snapshot.SerializeFloat(instance.m_fFloatValue);
		snapshot.SerializeString(instance.m_sStringValue);
		snapshot.SerializeString(instance.m_sSelectionKey);
		return true;
	}

	static void Encode(SSnapSerializerBase snapshot, ScriptCtx ctx, ScriptBitSerializer packet)
	{
		snapshot.EncodeInt(packet);
		snapshot.EncodeInt(packet);
		snapshot.EncodeBool(packet);
		snapshot.EncodeInt(packet);
		snapshot.EncodeFloat(packet);
		snapshot.EncodeString(packet);
		snapshot.EncodeString(packet);
	}

	static bool Decode(ScriptBitSerializer packet, ScriptCtx ctx, SSnapSerializerBase snapshot)
	{
		snapshot.DecodeInt(packet);
		snapshot.DecodeInt(packet);
		snapshot.DecodeBool(packet);
		snapshot.DecodeInt(packet);
		snapshot.DecodeFloat(packet);
		snapshot.DecodeString(packet);
		snapshot.DecodeString(packet);
		return true;
	}

	static bool SnapCompare(SSnapSerializerBase lhs, SSnapSerializerBase rhs, ScriptCtx ctx)
	{
		return lhs.CompareSnapshots(rhs, 4)
			&& lhs.CompareSnapshots(rhs, 4)
			&& lhs.CompareSnapshots(rhs, 1)
			&& lhs.CompareSnapshots(rhs, 4)
			&& lhs.CompareSnapshots(rhs, 4)
			&& lhs.CompareStringSnapshots(rhs)
			&& lhs.CompareStringSnapshots(rhs);
	}

	static bool PropCompare(GC_LobbyParameterValue instance, SSnapSerializerBase snapshot, ScriptCtx ctx)
	{
		return snapshot.CompareInt(instance.m_iDefinitionIndex)
			&& snapshot.CompareInt(instance.m_eType)
			&& snapshot.CompareBool(instance.m_bBoolValue)
			&& snapshot.CompareInt(instance.m_iIntValue)
			&& snapshot.CompareFloat(instance.m_fFloatValue)
			&& snapshot.CompareString(instance.m_sStringValue)
			&& snapshot.CompareString(instance.m_sSelectionKey);
	}
}

[BaseContainerProps()]
class GC_LobbyParameterCatalog
{
	static const int MAX_PARAMETERS = 32;
	static const int MAX_SELECTION_OPTIONS = 32;
	static const int MAX_STRING_VALUE_LENGTH = 256;
	static const int MAX_PAYLOAD_LENGTH = 256;
	static const int MAX_FLOAT_PRECISION = 6;

	[Attribute("", UIWidgets.Object, "Authoritative scenario parameter definitions")]
	protected ref array<ref GC_LobbyParameterDefinition> m_aDefinitions;

	protected ref array<ref GC_LobbyParameterValue> m_aValues = {};
	protected int m_iRevision;
	protected bool m_bValid = true;

	array<ref GC_LobbyParameterDefinition> GetDefinitions()
	{
		if (!m_aDefinitions)
			m_aDefinitions = {};

		return m_aDefinitions;
	}

	array<ref GC_LobbyParameterValue> GetValues() { return m_aValues; }
	int GetRevision() { return m_iRevision; }
	bool IsValid() { return m_bValid; }
	bool HasDefinitions() { return m_bValid && GetDefinitions().Count() > 0; }

	//! Validates static prefab data and creates defaults when the runtime has not
	//! yet been initialized. Invalid authoring disables the complete feature.
	bool Initialize()
	{
		m_bValid = ValidateAuthoring();
		if (!m_bValid)
		{
			m_aValues.Clear();
			return false;
		}

		if (!m_aValues || m_aValues.Count() != GetDefinitions().Count())
		{
			m_aValues = CreateDefaultValues();
			m_iRevision = 0;
		}

		return true;
	}

	array<ref GC_LobbyParameterValue> CreateDefaultValues()
	{
		array<ref GC_LobbyParameterValue> result = {};
		int definitionIndex;
		foreach (GC_LobbyParameterDefinition definition : GetDefinitions())
		{
			GC_LobbyParameterValue value = CreateValueForDefinition(definitionIndex, definition);
			if (value)
				result.Insert(value);
			definitionIndex++;
		}

		return result;
	}

	protected GC_LobbyParameterValue CreateValueForDefinition(int definitionIndex, GC_LobbyParameterDefinition definition)
	{
		if (!definition)
			return null;

		GC_LobbyBoolParameterDefinition boolDefinition = GC_LobbyBoolParameterDefinition.Cast(definition);
		if (boolDefinition)
		{
			GC_LobbyParameterValue boolValue = new GC_LobbyParameterValue();
			boolValue.SetBool(definitionIndex, boolDefinition.GetDefaultValue());
			return boolValue;
		}

		GC_LobbyIntegerParameterDefinition integerDefinition = GC_LobbyIntegerParameterDefinition.Cast(definition);
		if (integerDefinition)
		{
			GC_LobbyParameterValue integerValue = new GC_LobbyParameterValue();
			integerValue.SetInteger(definitionIndex, integerDefinition.GetDefaultValue());
			return integerValue;
		}

		GC_LobbyFloatParameterDefinition floatDefinition = GC_LobbyFloatParameterDefinition.Cast(definition);
		if (floatDefinition)
		{
			GC_LobbyParameterValue floatValue = new GC_LobbyParameterValue();
			floatValue.SetFloat(definitionIndex, floatDefinition.GetDefaultValue());
			return floatValue;
		}

		GC_LobbyStringParameterDefinition stringDefinition = GC_LobbyStringParameterDefinition.Cast(definition);
		if (stringDefinition)
		{
			GC_LobbyParameterValue stringValue = new GC_LobbyParameterValue();
			stringValue.SetString(definitionIndex, stringDefinition.GetDefaultValue());
			return stringValue;
		}

		GC_LobbySelectionParameterDefinition selectionDefinition = GC_LobbySelectionParameterDefinition.Cast(definition);
		if (selectionDefinition)
		{
			GC_LobbyParameterValue selectionValue = new GC_LobbyParameterValue();
			selectionValue.SetSelection(definitionIndex, selectionDefinition.GetDefaultKey());
			return selectionValue;
		}

		return null;
	}

	protected bool ValidateAuthoring()
	{
		array<ref GC_LobbyParameterDefinition> definitions = GetDefinitions();
		if (definitions.Count() > MAX_PARAMETERS)
		{
			PrintFormat("GC | Parameters | catalog rejected: parameter count=%1", definitions.Count(), level: LogLevel.ERROR);
			return false;
		}

		array<string> effectOwners = {};
		foreach (GC_LobbyParameterDefinition definition : definitions)
		{
			if (!definition)
			{
				Print("GC | Parameters | catalog rejected: null parameter definition", LogLevel.ERROR);
				return false;
			}

			if (definition.GetLabel().Length() > GC_LobbyParameterDefinition.MAX_LABEL_LENGTH
				|| definition.GetDescription().Length() > GC_LobbyParameterDefinition.MAX_DESCRIPTION_LENGTH
				|| !ValidateOwnerFactionKey(definition.GetOwnerFactionKey())
				|| !ValidateFactionKeys(definition.GetVisibleFactionKeys()))
			{
				PrintFormat("GC | Parameters | catalog rejected: metadata invalid label=%1", definition.GetLabel(), level: LogLevel.ERROR);
				return false;
			}

			if (!ValidateDefinition(definition))
				return false;

			if (definition.GetEffect() != GC_ELobbyParameterEffect.NONE)
			{
				GC_LobbyIntegerParameterDefinition integerDefinition = GC_LobbyIntegerParameterDefinition.Cast(definition);
				string owner = definition.GetOwnerFactionKey() + ":" + definition.GetEffect().ToString();
				if (!integerDefinition || integerDefinition.GetMinimum() < 0 || effectOwners.Contains(owner)
					|| definition.GetEffect() < GC_ELobbyParameterEffect.RESPAWN_BUDGET
					|| definition.GetEffect() > GC_ELobbyParameterEffect.RESPAWN_INTERVAL
					|| (definition.GetEffect() == GC_ELobbyParameterEffect.RESPAWN_INTERVAL && integerDefinition.GetMaximum() > 1440))
					return false;
				effectOwners.Insert(owner);
			}
		}

		return true;
	}

	protected bool ValidateOwnerFactionKey(string ownerFactionKey)
	{
		if (ownerFactionKey.IsEmpty())
			return false;

		if (ownerFactionKey.Length() > GC_LobbyParameterDefinition.MAX_KEY_LENGTH)
			return false;

		FactionManager factionManager = GetGame().GetFactionManager();
		return factionManager && factionManager.GetFactionByKey(ownerFactionKey);
	}

	protected bool ValidateFactionKeys(array<string> factionKeys)
	{
		if (!factionKeys || factionKeys.IsEmpty())
			return true;

		FactionManager factionManager = GetGame().GetFactionManager();
		if (!factionManager)
			return false;

		if (factionKeys.Count() > GC_LobbyParameterDefinition.MAX_FACTION_KEYS)
			return false;

		array<string> uniqueKeys = {};
		foreach (string key : factionKeys)
		{
			if (key.IsEmpty() || key.Length() > GC_LobbyParameterDefinition.MAX_KEY_LENGTH || uniqueKeys.Contains(key)
				|| !factionManager.GetFactionByKey(key))
				return false;

			uniqueKeys.Insert(key);
		}

		return true;
	}

	protected bool ValidateDefinition(GC_LobbyParameterDefinition definition)
	{
		GC_LobbyIntegerParameterDefinition integerDefinition = GC_LobbyIntegerParameterDefinition.Cast(definition);
		if (integerDefinition)
			return integerDefinition.GetMinimum() <= integerDefinition.GetMaximum()
				&& integerDefinition.GetStep() > 0
				&& (integerDefinition.GetMaximum() - integerDefinition.GetMinimum()) % integerDefinition.GetStep() == 0
				&& integerDefinition.GetDefaultValue() >= integerDefinition.GetMinimum()
				&& integerDefinition.GetDefaultValue() <= integerDefinition.GetMaximum()
				&& (integerDefinition.GetDefaultValue() - integerDefinition.GetMinimum()) % integerDefinition.GetStep() == 0;

		GC_LobbyFloatParameterDefinition floatDefinition = GC_LobbyFloatParameterDefinition.Cast(definition);
		if (floatDefinition)
		{
			if (floatDefinition.GetStep() <= 0 || floatDefinition.GetPrecision() < 0 || floatDefinition.GetPrecision() > MAX_FLOAT_PRECISION)
				return false;
			float remainder = (floatDefinition.GetDefaultValue() - floatDefinition.GetMinimum()) / floatDefinition.GetStep();
			return floatDefinition.GetMinimum() <= floatDefinition.GetMaximum()
				&& floatDefinition.GetDefaultValue() >= floatDefinition.GetMinimum()
				&& floatDefinition.GetDefaultValue() <= floatDefinition.GetMaximum()
				&& Math.AbsFloat(remainder - Math.Round(remainder)) < 0.001;
		}

		GC_LobbyStringParameterDefinition stringDefinition = GC_LobbyStringParameterDefinition.Cast(definition);
		if (stringDefinition)
			return stringDefinition.GetMaximumLength() >= 0
				&& stringDefinition.GetMaximumLength() <= MAX_STRING_VALUE_LENGTH
				&& stringDefinition.GetDefaultValue().Length() <= stringDefinition.GetMaximumLength();

		GC_LobbySelectionParameterDefinition selectionDefinition = GC_LobbySelectionParameterDefinition.Cast(definition);
		if (selectionDefinition)
		{
			array<ref GC_LobbyParameterOption> options = selectionDefinition.GetOptions();
			if (options.Count() <= 0 || options.Count() > MAX_SELECTION_OPTIONS)
				return false;

			array<string> keys = {};
			bool hasDefault;
			foreach (GC_LobbyParameterOption option : options)
			{
				if (!option || option.GetKey().IsEmpty() || option.GetKey().Length() > GC_LobbyParameterDefinition.MAX_KEY_LENGTH
					|| option.GetLabel().Length() > GC_LobbyParameterDefinition.MAX_LABEL_LENGTH
					|| option.GetPayload().Length() > MAX_PAYLOAD_LENGTH || keys.Contains(option.GetKey()))
					return false;

				keys.Insert(option.GetKey());
				if (option.GetKey() == selectionDefinition.GetDefaultKey())
					hasDefault = true;
			}

			return hasDefault;
		}

		return GC_LobbyBoolParameterDefinition.Cast(definition) != null;
	}

	//! Validates every submitted value against the authored catalog before any
	//! value is changed. This preserves atomicity on malformed requests.
	bool ValidateValues(array<ref GC_LobbyParameterValue> values)
	{
		if (!m_bValid || !values || values.Count() != GetDefinitions().Count())
			return false;

		array<bool> indices = {};
		indices.Resize(GetDefinitions().Count());
		foreach (GC_LobbyParameterValue value : values)
		{
			if (!value || value.GetDefinitionIndex() < 0 || value.GetDefinitionIndex() >= GetDefinitions().Count()
				|| indices[value.GetDefinitionIndex()])
				return false;

			GC_LobbyParameterDefinition definition = FindDefinition(value.GetDefinitionIndex());
			if (!definition || !ValidateValue(definition, value))
				return false;

			indices[value.GetDefinitionIndex()] = true;
		}

		foreach (bool present : indices)
		{
			if (!present)
				return false;
		}

		return true;
	}

	protected bool ValidateValue(GC_LobbyParameterDefinition definition, GC_LobbyParameterValue value)
	{
		if (GC_LobbyBoolParameterDefinition.Cast(definition))
			return value.GetType() == GC_ELobbyParameterType.BOOL;

		GC_LobbyIntegerParameterDefinition integerDefinition = GC_LobbyIntegerParameterDefinition.Cast(definition);
		if (integerDefinition)
		{
			int candidate = value.GetIntValue();
			return value.GetType() == GC_ELobbyParameterType.INTEGER
				&& candidate >= integerDefinition.GetMinimum()
				&& candidate <= integerDefinition.GetMaximum()
				&& (candidate - integerDefinition.GetMinimum()) % integerDefinition.GetStep() == 0;
		}

		GC_LobbyFloatParameterDefinition floatDefinition = GC_LobbyFloatParameterDefinition.Cast(definition);
		if (floatDefinition)
		{
			float candidate = value.GetFloatValue();
			float remainder = (candidate - floatDefinition.GetMinimum()) / floatDefinition.GetStep();
			return value.GetType() == GC_ELobbyParameterType.FLOAT
				&& candidate >= floatDefinition.GetMinimum()
				&& candidate <= floatDefinition.GetMaximum()
				&& Math.AbsFloat(remainder - Math.Round(remainder)) < 0.001;
		}

		GC_LobbyStringParameterDefinition stringDefinition = GC_LobbyStringParameterDefinition.Cast(definition);
		if (stringDefinition)
			return value.GetType() == GC_ELobbyParameterType.STRING
				&& value.GetStringValue().Length() <= stringDefinition.GetMaximumLength();

		GC_LobbySelectionParameterDefinition selectionDefinition = GC_LobbySelectionParameterDefinition.Cast(definition);
		if (selectionDefinition && value.GetType() == GC_ELobbyParameterType.SELECTION)
		{
			foreach (GC_LobbyParameterOption option : selectionDefinition.GetOptions())
			{
				if (option && option.GetKey() == value.GetSelectionKey())
					return true;
			}
		}

		return false;
	}

	static bool AreValuesEqual(GC_LobbyParameterValue left, GC_LobbyParameterValue right)
	{
		if (!left || !right || left.GetDefinitionIndex() != right.GetDefinitionIndex() || left.GetType() != right.GetType())
			return false;

		switch (left.GetType())
		{
			case GC_ELobbyParameterType.BOOL: return left.GetBoolValue() == right.GetBoolValue();
			case GC_ELobbyParameterType.INTEGER: return left.GetIntValue() == right.GetIntValue();
			case GC_ELobbyParameterType.FLOAT: return Math.AbsFloat(left.GetFloatValue() - right.GetFloatValue()) < 0.0001;
			case GC_ELobbyParameterType.STRING: return left.GetStringValue() == right.GetStringValue();
			case GC_ELobbyParameterType.SELECTION: return left.GetSelectionKey() == right.GetSelectionKey();
		}

		return false;
	}

	GC_LobbyParameterDefinition FindDefinition(int definitionIndex)
	{
		if (definitionIndex < 0 || definitionIndex >= GetDefinitions().Count())
			return null;

		return GetDefinitions()[definitionIndex];
	}

	GC_LobbyParameterValue FindValue(int definitionIndex)
	{
		foreach (GC_LobbyParameterValue value : m_aValues)
		{
			if (value && value.GetDefinitionIndex() == definitionIndex)
				return value;
		}

		return null;
	}

	bool ApplyValues(array<ref GC_LobbyParameterValue> values)
	{
		if (!ValidateValues(values))
			return false;

		m_aValues.Clear();
		for (int definitionIndex; definitionIndex < GetDefinitions().Count(); definitionIndex++)
		{
			GC_LobbyParameterValue submittedValue;
			foreach (GC_LobbyParameterValue candidate : values)
			{
				if (candidate && candidate.GetDefinitionIndex() == definitionIndex)
				{
					submittedValue = candidate;
					break;
				}
			}
			GC_LobbyParameterValue value = new GC_LobbyParameterValue();
			value.CopyFrom(submittedValue);
			m_aValues.Insert(value);
		}

		m_iRevision++;
		return true;
	}

	bool ApplySnapshot(int revision, array<ref GC_LobbyParameterValue> values)
	{
		if (revision <= m_iRevision || !ValidateValues(values))
			return false;

		m_aValues.Clear();
		for (int definitionIndex; definitionIndex < GetDefinitions().Count(); definitionIndex++)
		{
			GC_LobbyParameterValue snapshotValue;
			foreach (GC_LobbyParameterValue candidate : values)
			{
				if (candidate && candidate.GetDefinitionIndex() == definitionIndex)
				{
					snapshotValue = candidate;
					break;
				}
			}
			GC_LobbyParameterValue value = new GC_LobbyParameterValue();
			value.CopyFrom(snapshotValue);
			m_aValues.Insert(value);
		}

		m_iRevision = revision;
		return true;
	}

	bool Save(ScriptBitWriter writer)
	{
		writer.WriteInt(m_iRevision);
		writer.WriteInt(m_aValues.Count());
		foreach (GC_LobbyParameterValue value : m_aValues)
		{
			if (!value || !value.RplSave(writer))
				return false;
		}

		return true;
	}

	bool Load(ScriptBitReader reader)
	{
		int revision;
		int valueCount;
		if (!reader.ReadInt(revision))
		{
			Print("GC | JIP load parameters failed: field=revision", level: LogLevel.ERROR);
			return false;
		}
		if (revision < 0)
		{
			PrintFormat("GC | JIP load parameters failed: invalid revision=%1", revision, level: LogLevel.ERROR);
			return false;
		}
		if (!reader.ReadInt(valueCount))
		{
			Print("GC | JIP load parameters failed: field=valueCount", level: LogLevel.ERROR);
			return false;
		}
		if (valueCount < 0 || valueCount > MAX_PARAMETERS)
		{
			PrintFormat("GC | JIP load parameters failed: invalid valueCount=%1", valueCount, level: LogLevel.ERROR);
			return false;
		}

		array<ref GC_LobbyParameterValue> values = {};
		for (int index; index < valueCount; index++)
		{
			GC_LobbyParameterValue value = new GC_LobbyParameterValue();
			if (!value.RplLoad(reader))
			{
				PrintFormat("GC | JIP load parameters failed: value=%1 count=%2", index, valueCount, level: LogLevel.ERROR);
				return false;
			}

			values.Insert(value);
		}

		if (!m_bValid)
		{
			m_aValues = values;
			m_iRevision = revision;
			return true;
		}

		if (values.Count() != GetDefinitions().Count())
		{
			PrintFormat("GC | JIP load parameters failed: valueCount=%1 definitionCount=%2 catalogValid=%3", values.Count(),
				GetDefinitions().Count(), m_bValid, level: LogLevel.ERROR);
			return false;
		}

		if (!ValidateValues(values))
		{
			foreach (GC_LobbyParameterValue value : values)
			{
				GC_LobbyParameterDefinition definition;
				if (value)
					definition = FindDefinition(value.GetDefinitionIndex());
				PrintFormat("GC | JIP load parameters rejected: index=%1 type=%2 definition=%3 valueValid=%4", value.GetDefinitionIndex(),
					value.GetType(), definition != null, definition && ValidateValue(definition, value), level: LogLevel.ERROR);
			}
			return false;
		}

		m_aValues = values;
		m_iRevision = revision;
		return true;
	}

	void Clear()
	{
		m_aValues.Clear();
		m_iRevision = 0;
	}
}
