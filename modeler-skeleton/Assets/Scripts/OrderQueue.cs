using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns soup order tickets as world-space sprites pinned to a bulletin board.
///
/// queueParent should be a Transform on the in-world bulletin board. Order
/// prefabs are SpriteRenderer-based (no Canvas). They get stacked along
/// queueParent's local axes using orderSpacing. Each ticket also gets a
/// generated world-space timer bar (background + shrinking fill) drawn from
/// runtime-created 1x1 white sprites — no sprite assets required.
/// </summary>
public class OrderQueue : MonoBehaviour
{
    public static OrderQueue Instance { get; private set; }

    [Tooltip("Fallback prefab used if a per-soup prefab below is not assigned.")]
    public GameObject orderPrefab;

    [Header("Per-Soup Ticket Prefabs")]
    [Tooltip("Order ticket prefab shown when a Carrot soup order is rolled.")]
    public GameObject carrotOrderPrefab;
    [Tooltip("Order ticket prefab shown when an Onion soup order is rolled.")]
    public GameObject onionOrderPrefab;
    [Tooltip("Order ticket prefab shown when a Tomato soup order is rolled.")]
    public GameObject tomatoOrderPrefab;

    [Tooltip("The bulletin board (or any world-space anchor) that tickets pin to. " +
             "Tickets become children of this transform and inherit its rotation/scale.")]
    public Transform queueParent;
    public float[] spawnTimes = { 5f, 23f, 30f };

    [Header("Layout (world units, in queueParent's local space)")]
    [Tooltip("Per-slot offset from queueParent's origin within a row. e.g. (0.4, 0, 0) for a horizontal row along the board.")]
    public Vector3 orderSpacing = new Vector3(0.4f, 0f, 0f);
    [Tooltip("Offset added when starting a new row. e.g. (0, -0.3, 0) drops the next row below the previous one.")]
    public Vector3 rowSpacing = new Vector3(0f, -0.3f, 0f);
    [Tooltip("How many orders fit in one row before wrapping to the next. Set to 0 or less to disable wrapping (single endless row).")]
    public int ordersPerRow = 4;

    // Monotonically increasing slot index — tickets keep their position even
    // when earlier ones are fulfilled, so the queue doesn't reflow mid-game.
    private int spawnSlot;

    [Header("Order Lifetime")]
    [Tooltip("How long (seconds) a freshly spawned order has before it expires.")]
    public float orderLifetime = 60f;
    [Tooltip("Below this many seconds remaining, the timer bar switches to warningColor.")]
    public float warningThreshold = 10f;

    [Header("Timer Bar Colors")]
    [Tooltip("Background cube color, applied once at spawn to the OrderTicket.backgroundRenderer if set.")]
    public Color timerBarBackgroundColor = Color.black;
    [Tooltip("Fill cube color while there's plenty of time left.")]
    public Color timerBarFillColor = Color.green;
    [Tooltip("Fill cube color once remaining time drops below warningThreshold.")]
    public Color timerBarWarningColor = Color.red;

    // Result of a delivery attempt against the queue. Lets the caller log
    // and react without OrderQueue needing to know about logging conventions.
    public struct DeliveryResult
    {
        public bool fulfilled;
        public int pointsAwarded;     // 0 when fulfilled == false
        public VegetableType soupType;
    }

    private class OrderTimer
    {
        public Transform fillAnchor;        // localScale.x = originalFillScale.x * fraction
        public Vector3 originalFillScale;   // captured at spawn so prefab dimensions are preserved
        public Renderer fillRenderer;       // recolored on warning threshold
        public float remaining;
        public float lifetime;
        public VegetableType soupType;
        public GameObject ticketObject;
    }

    private readonly List<OrderTimer> timers = new List<OrderTimer>();
    private int nextIndex;
    private float t;

    private void Awake()
    {
        Instance = this;
    }

    void Update()
    {
        t += Time.deltaTime;
        if (nextIndex < spawnTimes.Length && t >= spawnTimes[nextIndex])
        {
            SpawnOrder();
            nextIndex++;
        }

        // Iterate backwards so we can remove expired entries safely.
        for (int i = timers.Count - 1; i >= 0; i--)
        {
            OrderTimer timer = timers[i];

            timer.remaining -= Time.deltaTime;
            if (timer.remaining <= 0f)
            {
                ExpireOrder(i);
                continue;
            }

            if (timer.fillAnchor == null) continue;

            float fraction = timer.remaining / timer.lifetime;
            // Shrink the fill from the right by squeezing its anchor's X scale.
            // Multiply against the *original* X so the prefab's own bar
            // dimensions are preserved at fraction = 1.
            Vector3 orig = timer.originalFillScale;
            timer.fillAnchor.localScale = new Vector3(orig.x * fraction, orig.y, orig.z);

            if (timer.fillRenderer != null)
                timer.fillRenderer.material.color = timer.remaining < warningThreshold ? timerBarWarningColor : timerBarFillColor;
        }
    }

    // Fulfill the oldest matching order (lowest remaining time wins) and
    // award score for it. Returns a DeliveryResult describing the outcome.
    public DeliveryResult TryFulfillOrder(VegetableType type)
    {
        int oldestIndex = -1;
        float minRemaining = float.MaxValue;
        for (int i = 0; i < timers.Count; i++)
        {
            if (timers[i].soupType == type && timers[i].remaining < minRemaining)
            {
                minRemaining = timers[i].remaining;
                oldestIndex = i;
            }
        }
        if (oldestIndex < 0) return new DeliveryResult { fulfilled = false, soupType = type };

        OrderTimer match = timers[oldestIndex];
        float fraction = match.lifetime > 0f ? match.remaining / match.lifetime : 0f;

        int points = 0;
        if (ScoreManager.Instance != null)
            points = ScoreManager.Instance.RegisterDelivery(fraction);

        if (match.ticketObject != null) Destroy(match.ticketObject);
        timers.RemoveAt(oldestIndex);
        ReflowQueue();

        return new DeliveryResult { fulfilled = true, pointsAwarded = points, soupType = type };
    }

    private GameObject PickPrefabFor(VegetableType type)
    {
        GameObject chosen = null;
        switch (type)
        {
            case VegetableType.Carrot: chosen = carrotOrderPrefab; break;
            case VegetableType.Onion:  chosen = onionOrderPrefab;  break;
            case VegetableType.Tomato: chosen = tomatoOrderPrefab; break;
        }
        return chosen != null ? chosen : orderPrefab;
    }

    private void ExpireOrder(int index)
    {
        OrderTimer timer = timers[index];
        if (timer.ticketObject != null) Destroy(timer.ticketObject);
        timers.RemoveAt(index);
        if (ScoreManager.Instance != null) ScoreManager.Instance.RegisterMiss();
        Debug.Log($"[OrderQueue] {timer.soupType} order expired");
        ReflowQueue();
    }

    // After any removal, recompute every live ticket's localPosition so the
    // queue closes up the gap (slot 0 always at queueParent origin, slot 1 at
    // orderSpacing, etc.). Also rewinds spawnSlot so the next SpawnOrder lands
    // at the trailing end rather than skipping a slot.
    private void ReflowQueue()
    {
        int perRow = ordersPerRow > 0 ? ordersPerRow : int.MaxValue;
        for (int i = 0; i < timers.Count; i++)
        {
            OrderTimer t = timers[i];
            if (t.ticketObject == null) continue;
            int col = i % perRow;
            int row = i / perRow;
            t.ticketObject.transform.localPosition = orderSpacing * col + rowSpacing * row;
        }
        spawnSlot = timers.Count;
    }

    private void SpawnOrder()
    {
        if (queueParent == null)
        {
            Debug.LogWarning("[OrderQueue] queueParent is not assigned — drop the bulletin board (or a child anchor on it) here.");
            return;
        }

        VegetableType type = (VegetableType)Random.Range(0, System.Enum.GetValues(typeof(VegetableType)).Length);
        GameObject prefab = PickPrefabFor(type);
        if (prefab == null)
        {
            Debug.LogWarning($"[OrderQueue] No prefab assigned for {type} (and no fallback orderPrefab). Skipping spawn.");
            return;
        }

        GameObject go = Instantiate(prefab, queueParent);

        // Slot → (column, row) so the queue wraps to a new row after
        // ordersPerRow tickets. ordersPerRow <= 0 disables wrapping.
        int perRow = ordersPerRow > 0 ? ordersPerRow : int.MaxValue;
        int col = spawnSlot % perRow;
        int row = spawnSlot / perRow;
        go.transform.localPosition = orderSpacing * col + rowSpacing * row;
        go.transform.localRotation = Quaternion.identity;
        spawnSlot++;

        // The prefab owns its bar layout; we just animate what it points at.
        OrderTicket ticket = go.GetComponent<OrderTicket>();
        Transform fillAnchor = null;
        Renderer fillRenderer = null;
        Vector3 originalFillScale = Vector3.one;
        if (ticket != null)
        {
            fillAnchor = ticket.fillAnchor;
            fillRenderer = ticket.fillRenderer;
            if (fillAnchor != null) originalFillScale = fillAnchor.localScale;
            if (ticket.backgroundRenderer != null)
                ticket.backgroundRenderer.material.color = timerBarBackgroundColor;
            if (fillRenderer != null)
                fillRenderer.material.color = timerBarFillColor;
            if (fillAnchor == null)
                Debug.LogWarning($"[OrderQueue] {prefab.name}'s OrderTicket has no fillAnchor wired — the timer bar will not animate.");
        }
        else
        {
            Debug.LogWarning($"[OrderQueue] {prefab.name} has no OrderTicket component — the timer bar will not animate. " +
                             "Add OrderTicket to the prefab and wire fillAnchor / fillRenderer.");
        }

        timers.Add(new OrderTimer
        {
            fillAnchor = fillAnchor,
            originalFillScale = originalFillScale,
            fillRenderer = fillRenderer,
            remaining = orderLifetime,
            lifetime = orderLifetime,
            soupType = type,
            ticketObject = go
        });
    }
}
