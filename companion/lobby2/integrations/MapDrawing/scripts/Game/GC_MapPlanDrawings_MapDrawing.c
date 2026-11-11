//! Optional dependency integration; uses Till W's native replication and JIP cache.
modded class GC_MapPlanDrawings
{
	//------------------------------------------------------------------------------------------------
	override bool IsAvailable()
	{
		return TILW_DrawingSyncComponent.FindInstance() != null;
	}

	//------------------------------------------------------------------------------------------------
	override void Capture(array<ref array<int>> drawings)
	{
		TILW_DrawingSyncComponent sync = TILW_DrawingSyncComponent.FindInstance();
		if (sync)
			sync.GC_CapturePlan(drawings);
	}

	//------------------------------------------------------------------------------------------------
	override bool Validate(array<ref array<int>> drawings)
	{
		if (!drawings || drawings.Count() > 256)
			return false;
		if (!TILW_DrawingSyncComponent.FindInstance())
			return drawings.IsEmpty();
		foreach (array<int> row : drawings)
		{
			// Properties followed by 2..255 packed points, as required by native JIP.
			if (!row || row.Count() < 3 || row.Count() > 256)
				return false;
		}
		return true;
	}

	//------------------------------------------------------------------------------------------------
	override void Replace(array<ref array<int>> drawings)
	{
		TILW_DrawingSyncComponent sync = TILW_DrawingSyncComponent.FindInstance();
		if (sync)
			sync.GC_ReplacePlan(drawings);
	}
}

modded class TILW_DrawingSyncComponent
{
	//------------------------------------------------------------------------------------------------
	void GC_CapturePlan(array<ref array<int>> drawings)
	{
		foreach (array<int> cached : m_aDrawingsCache)
		{
			int count = cached.Count();
			array<int> row = {cached[count - 2]};
			for (int index; index < count - 2; index++)
				row.Insert(cached[index]);
			drawings.Insert(row);
		}
	}

	//------------------------------------------------------------------------------------------------
	void GC_ReplacePlan(array<ref array<int>> drawings)
	{
		if (!GC_MapPlans.GetInstance() || GC_LobbyPhase.ArePlayerMapMarkersLocked())
			return;
		array<int> identifiers = {};
		foreach (array<int> cached : m_aDrawingsCache)
			identifiers.Insert(cached[cached.Count() - 1]);
		foreach (int identifier : identifiers)
			BroadcastRemoval(identifier);
		foreach (array<int> row : drawings)
		{
			array<int> points = {};
			for (int index = 1; index < row.Count(); index++)
				points.Insert(row[index]);
			// The native method assigns a new drawing ID and consumes the points.
			BroadcastDrawing(0, row[0], points);
		}
	}

	//------------------------------------------------------------------------------------------------
	override void BroadcastDrawing(int identifier, int properties, array<int> points)
	{
		if (GC_LobbyPhase.ArePlayerMapMarkersLocked())
			return;
		super.BroadcastDrawing(identifier, properties, points);
		GC_MapPlans plans = GC_MapPlans.GetInstance();
		if (plans)
			plans.Changed();
	}

	//------------------------------------------------------------------------------------------------
	override void BroadcastRemoval(int identifier)
	{
		super.BroadcastRemoval(identifier);
		GC_MapPlans plans = GC_MapPlans.GetInstance();
		if (plans)
			plans.Changed();
	}
}

modded class TILW_DrawingSystem
{
	//------------------------------------------------------------------------------------------------
	override protected void OnStartDrawing(float value, EActionTrigger reason)
	{
		if (!GC_LobbyPhase.ArePlayerMapMarkersLocked())
			super.OnStartDrawing(value, reason);
	}

	//------------------------------------------------------------------------------------------------
	override protected void OnStopDrawing(float value, EActionTrigger reason)
	{
		if (GC_LobbyPhase.ArePlayerMapMarkersLocked())
		{
			if (m_aDrawCommands)
				RemovePreview();
			else
				m_PreviewDrawing = null;
			m_aDrawPoints = null;
			return;
		}
		super.OnStopDrawing(value, reason);
	}
}
