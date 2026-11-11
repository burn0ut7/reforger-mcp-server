//! Explicit development-only checks of the production death and camera handoff.
modded class GC_LobbyPlayerController
{
	protected bool m_bGC_DeathCameraTestArmed;
	protected vector m_vGC_TestEyePosition;
	protected vector m_vGC_TestViewDirection;
	protected string m_sGC_DeathCameraResult;

	string GC_TestDeathCamera(bool rolledSample)
	{
		ChimeraCharacter character = ChimeraCharacter.Cast(GetControlledEntity());
		if (!Replication.IsServer() || GetGame().GetPlayerController() != this || !character
			|| character.FindComponent(GC_LobbySpectatorBodyComponent) || !m_bLastCharacterViewValid)
			return "BLOCKED: living local character and native camera sample required";
		if (rolledSample)
		{
			vector transform[4];
			Math3D.AnglesToMatrix("123 -35 28", transform);
			transform[3] = character.EyePosition();
			GC_PublishNetworkCameraTransform(character, transform, transform);
		}
		m_vGC_TestEyePosition = m_vLastCharacterEyePosition;
		m_vGC_TestViewDirection = m_vLastCharacterViewDirection;
		m_sGC_DeathCameraResult = "PENDING";
		m_bGC_DeathCameraTestArmed = true;
		character.GetCharacterController().ForceDeath();
		return string.Format("death requested; networkSubscriber=%1 rolledSample=%2", m_bPublishNetworkCamera, rolledSample);
	}

	override protected void CreateSpectatorCamera(vector position = vector.Zero, vector forward = vector.Zero, vector up = vector.Zero)
	{
		super.CreateSpectatorCamera(position, forward, up);
		if (!m_bGC_DeathCameraTestArmed || !m_SpectatorCamera)
			return;
		m_bGC_DeathCameraTestArmed = false;
		vector transform[4];
		m_SpectatorCamera.GetWorldTransform(transform);
		float positionError = vector.Distance(transform[3], m_vGC_TestEyePosition);
		float directionError = vector.Distance(transform[2], m_vGC_TestViewDirection);
		float rollError = Math.AbsFloat(transform[0][1]);
		bool passed = positionError < 0.01 && directionError < 0.001 && rollError < 0.001 && !m_bLastCharacterViewValid;
		m_sGC_DeathCameraResult = string.Format("passed=%1 positionError=%2 directionError=%3 rollError=%4 consumed=%5", passed, positionError, directionError, rollError, !m_bLastCharacterViewValid);
		Print("GC | Death Camera Test | " + m_sGC_DeathCameraResult);
	}

	string GC_GetDeathCameraTestResult()
	{
		return m_sGC_DeathCameraResult;
	}
}

modded class GC_LobbyManager
{
	string GC_RunDeathCameraTest(bool rolledSample)
	{
		GC_LobbyPlayerController controller = GC_LobbyPlayerController.Cast(GetGame().GetPlayerController());
		if (!controller || GetLobbyPhase() != SCR_EGameModeState.GAME)
			return "BLOCKED: local match required";
		return controller.GC_TestDeathCamera(rolledSample);
	}

	string GC_ReadDeathCameraTest()
	{
		GC_LobbyPlayerController controller = GC_LobbyPlayerController.Cast(GetGame().GetPlayerController());
		if (!controller)
			return "BLOCKED: no local controller";
		return controller.GC_GetDeathCameraTestResult();
	}
}
