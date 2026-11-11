# Area capture

Add **Area Condition** to a mission event or flag. Keep using a named Radius Area
or closed polygon. Add its optional **Capture** object to turn the immediate
area answer into a timed captured/not-captured answer. Leaving Capture unset
preserves existing area behavior.

## Players, objects and vehicles

Capture timing works with either target selection:

- **Players / AI:** living connected players, registered AI or both, with optional
  faction filters. Use Count, Proportion or Local faction share.
- **Named Entities:** any uniquely named world entities, including vehicles,
  cargo, props, characters or mixed lists. Use Count or Proportion. For example,
  list four supply trucks and use Proportion 0.75 with Capture Seconds 20 to
  require at least three trucks in the area for 20 seconds. Duplicate names
  count once. Every name must resolve; missing/deleted targets suspend capture
  instead of reducing the denominator. A replacement with the same name is
  resolved on a later check. Destroyed targets still count while they exist.

Inside and Outside use the same capture/loss timing. Capture measures this
area requirement; it does not turn unrelated event conditions into timed
capture inputs. Combine conditions using the existing event/flag groups when
additional requirements are needed.

Area sampling uses cached named references and native player/AI registries,
then direct point-in-area checks. It never searches world entities by sphere,
box or another spatial enumeration. Counts are sampled together and reused
by equivalent selections; Capture timing remains independent per condition.

## Factions and contesting

Choose **Players / AI** targets and name the capturing faction(s). Choose
**Local faction share** as the Requirement:

- The numerator is the selected factions' living characters on the selected
  side of the area. Multiple selected factions form one combined force.
- The denominator is all eligible living characters with a faction on that
  side, except **Ignored Factions**. Other factions compete regardless of
  diplomatic relations. Spectators, dead units and empty playable slots do not
  count. Players / AI selection applies to both numerator and denominator.
- Share **1** requires uncontested occupation; **0.7** allows capture with at
  least 70% of the local force. **At Most** reverses the comparison to <=.
  An empty area fails At Least and passes At Most, matching TilW's faction
  trigger. Capturing factions cannot also be ignored.

For separate US, USSR and FIA objectives, create one Area Condition per faction,
all referencing the same area. Their capture memories are independent, as in
TilW. There is no exclusive objective-owner system: thresholds of 0.5 can let
both sides pass at a tie. Use a threshold above 0.5 for majority control, or 1
for no opponents. This does not change native faction membership or relations.

**Proportion** remains the extraction requirement: e.g. 90% of all currently
living US players inside the extraction area, or a proportion of listed named
entities. **Count** is suitable for named-object presence or fixed numbers of
players/AI. Both can use the same
Capture settings. Local share is a separate requirement so existing extraction
missions do not silently change meaning.

## Capture settings

| Setting | Behavior |
| --- | --- |
| Seconds | Time needed for a changed requirement to commit. Applies to both capture and loss. Zero commits immediately at a check. |
| Check Interval | Whole seconds between capture checks, minimum 1. At 10 seconds duration and 2 seconds interval, capture needs five ticks after discovery. Nonmultiples round up to the next check. Delayed server updates contribute at most one tick; unobserved time is not caught up. |
| Interruption | When the requirement returns to the committed state, Roll back removes pending progress on every tick, Hold preserves it, and Reset clears it. Applies equally to interrupted capture and interrupted loss. |
| Skip Discovery | Off: first valid Match observation silently establishes the initial answer immediately. On: start false and earn the full capture duration. |
| Stop After First Change | Freeze after the first completed transition. Discovery does not count; the transition may be a capture or a loss. |
| Messages | Optional. Unset means silent. Provides six separately editable texts: captured, capturing, capture interrupted, lost, losing, loss interrupted. Blank text suppresses that transition. |

For Local faction share with At Most checked, capture/loss message directions
are reversed, matching TilW: a low share meeting the condition announces the
selected faction losing the area, not capturing it. The condition result itself
remains the authored <= comparison.

Messages use `%1` for the selected faction display names and `%2` for Location.
Recipient factions are independent of capturing/ignored factions; empty means
everyone, including spectators. They use Lobby2's existing **Objective Status**
notification queue and lifetime, rather than introducing TilW's separate
five-second hint display. Messages occur only on status transitions, including
resuming interrupted progress; discovery is silent. Messages are not replayed
to JIP clients. Text and expanded-message limits use the existing notification
validation; rejected messages are logged without cancelling capture.

Capture runs only during Match. Missing geometry/population fails closed,
including under a None condition, and clears pending progress without awarding
unobserved time. A committed result is retained internally while unavailable.
The first available sample after an interruption can resume checking; the
existing committed result is not treated as fresh discovery. Framework shutdown
discards all capture memory. Resetting a mission event does not reset its area
condition's capture memory.

Use the captured condition to run normal mission actions. For loss actions,
put the same configured capture condition under **Group Condition > None**.
For persistent mission flags, use normal Flag Action Set/Reset operations in
capture/loss events; flags retain their existing remembered-count semantics.
Copying a capture definition creates independent memory, so keep timing,
targets and discovery settings consistent for paired capture/loss events.

## Example

Area `Depot`, Players / AI targets `US`, Local faction share `1`, ignored faction
`CIV`, Capture Seconds `30`, Check Interval `1`, Skip Discovery checked, and
Interruption Roll back: US needs 30 qualifying ticks. Any counted opponent
interrupts the attempt and removes one second of pending progress per tick.
After capture, 30 failing ticks lose the objective. Optional messages announce
starts, interruptions and completed changes. Use normal event repeatability to
decide whether the associated actions may run again.
