using System.Text;
using TMPro;
using UnityEngine;

/// <summary>F3 opt-in viva display. Aiming at an enemy selects it; otherwise inspect the nearest living agent.</summary>
public sealed class AIDebugPresenter : MonoBehaviour
{
    [SerializeField]
    private EnemyWorld world;
    [SerializeField]
    private Camera viewCamera;
    [SerializeField]
    private GameObject panel;
    [SerializeField]
    private TMP_Text output;
    [SerializeField]
    private bool visible;
    private float nextRefresh;
    private readonly StringBuilder text = new StringBuilder(1800);
    private void OnEnable()
    {
        output.enableAutoSizing = true;
        output.fontSizeMin = 15;
        output.fontSizeMax = 22;
        panel.SetActive(visible);
        nextRefresh = 0;
    }

    private void OnDisable()
    {
        if (panel != null)
            panel.SetActive(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F3))
        {
            visible = !visible;
            panel.SetActive(visible);
            nextRefresh = 0;
        }

        if (!visible || Time.unscaledTime < nextRefresh)
            return;
        nextRefresh = Time.unscaledTime + .25f;
        EnemyActor selected = null;
        if (Physics.Raycast(viewCamera.ViewportPointToRay(new Vector3(.5f, .5f)), out var hit, 100, 1 << 9, QueryTriggerInteraction.Ignore))
            selected = hit.collider.GetComponentInParent<EnemyActor>();
        if (selected == null)
        {
            float nearest = float.PositiveInfinity;
            foreach (var actor in world.Living)
            {
                if (actor == null)
                    continue;
                float distance = (actor.transform.position - world.player.transform.position).sqrMagnitude;
                if (distance < nearest)
                {
                    nearest = distance;
                    selected = actor;
                }
            }
        }

        text.Clear();
        text.AppendLine("AI INSPECTOR  /  F3 TO CLOSE");
        text.Append("Living: ").Append(world.Living.Count).AppendLine("   Aim at an agent to inspect");
        if (selected == null)
        {
            text.AppendLine("Waiting for a living agent.");
            output.text = text.ToString();
            return;
        }

        var senses = selected.Perception;
        text.AppendLine(selected.name.Replace("(Clone)", ""));
        text.AppendLine(selected.Decision);
        text.Append("Health ").Append(selected.Health.Current.ToString("0")).Append(" | Distance ")
            .Append(senses.HasMemory ? senses.Distance.ToString("0.0") + " m" : "unknown").AppendLine();
        text.Append("LOS ").Append(senses.Visible).Append(" | Heard ").Append(senses.Heard).AppendLine();
        text.Append("Player weakness ").Append(senses.PlayerWeakness.ToString("0.00")).Append(" | ").Append(senses.Weapon).AppendLine();
        text.Append("Player firing ").Append(senses.PlayerFiring).AppendLine();
        text.Append(senses.Knowledge).Append(" | Memory ").Append(senses.MemoryRemaining.ToString("0.0")).AppendLine(" s");
        text.Append("Observed reload ").Append(senses.PlayerReloading).Append(" | Rally ").Append(selected.IsEncouraged).AppendLine();
        text.Append(selected.Combat.Phase).Append(" | Cooldown ").Append(selected.Combat.CooldownRemaining.ToString("0.0")).AppendLine(" s");
        text.Append("Attack slot ").Append(world.HasAttackSlot(selected)).Append(" | Active ")
            .Append(world.ActiveAttackers).Append('/').Append(world.MaximumAttackers).AppendLine();
        text.Append("Navigation: ").Append(selected.Movement.Status).Append(" | Retries ").Append(selected.Movement.RetryCount).AppendLine();
        if (selected.TryGetComponent<HunterBrain>(out var hunter))
        {
            text.AppendLine("MEMBER 1 / A*  f = g + h");
            text.Append("Expanded ").Append(hunter.ExpandedNodes).Append(" | Path cost ").Append(hunter.PathCost.ToString("0.0")).AppendLine();
        }

        if (selected.TryGetComponent<RunnerBrain>(out var runner))
        {
            text.AppendLine("MEMBER 2 / UTILITY + HYSTERESIS");
            text.Append("Reachable ").Append(runner.PathAvailable).Append(" | Shotgun threat ").Append(runner.ShotgunThreat.ToString("0.00")).AppendLine();
            Scores(new[] { "Chase", "Attack", "Caution", "Flank", "Disengage" }, runner.Scores);
        }

        if (selected.TryGetComponent<AmbusherBrain>(out var ambusher))
        {
            text.AppendLine("MEMBER 3 / TACTICAL POSITION SCORING");
            text.Append("Reserved ").Append(ambusher.Selected != null ? ambusher.Selected.name : "none").AppendLine();
            if (ambusher.PointScores != null)
                for (int i = 0, shown = 0; i < ambusher.PointScores.Length && shown < 5; i++)
                    if (world.tacticalPoints[i] != null)
                    {
                        text.Append(world.tacticalPoints[i].name).Append("  ").Append(ambusher.PointScores[i].ToString("0.00")).AppendLine();
                        shown++;
                    }
        }

        if (selected.TryGetComponent<EliteBrain>(out var elite))
        {
            text.AppendLine("MEMBER 4 / HIERARCHY + UTILITY");
            text.Append("Nearby allies ").Append(elite.NearbyAllies).AppendLine();
            text.Append("Cover ").Append(elite.Cover != null ? elite.Cover.name : "none").AppendLine();
            Scores(new[] { "Direct", "Heavy", "Push", "Distance", "Reposition" }, elite.Scores);
        }

        output.text = text.ToString();
    }

    private void Scores(string[] names, float[] values)
    {
        for (int i = 0; i < values.Length; i++)
            text.Append(names[i]).Append("  ").Append(values[i].ToString("0.00")).AppendLine();
    }
}
