# GOOSE Compact Operator UX — Windows Acceptance

Scope: GOOSE workspace only. Retain key-stable publisher selection, the bounded live and historical traffic views, the 8-DIP workspace gutters, raw MMS evidence, and the previously validated signal interpretation.

## Design behavior

- The source rail has one search field with local WPF vector Material-style search/close icons. An empty search hides the clear control; typing reveals it. Clicking clear resets the existing filter and restores focus to the search input, without rebuilding publisher identity or changing the selected publisher.
- The duplicate message-name dropdown and duplicate explanatory source footer are removed. The repeated-messages toggle still controls timeline visibility only.
- Only the source ListView scrolls. Its Grid star row constrains height and the ListView uses the WPF recyclable virtualizing stack; the title, search and retransmission toggle remain visible.
- The sticky Inspector status remains visible above the scrollable evidence. Within that evidence are three independent sections: Message (open), Network (closed) and Values (open). Users can expand/collapse each in any combination, using mouse, touch or keyboard. The static controls preserve their state through receive updates.
- Values have no forced maximum height. Closing Message/Network reveals more DataSet rows, and the single evidence scrollbar reveals overflowing content.
- The compact circle-chevron icon is drawn as vector geometry with no downloaded font, icon dependency or network access.
- Raw MMS types and true signal identity remain traceable from expanded entry detail; unconfigured bit patterns are not translated into physical switch states.

## Acceptance matrix

| Test | Expected |
| --- | --- |
| 20+ publishers at 768 px window height | Source ListView scrolls to last item; search and toggle remain fixed |
| Search by message/source/MAC, then clear | Matching rows update without reset of keyed publisher; clear icon disappears on empty |
| Search miss, then clear | No false publisher removal; all sources return |
| Expand Message + Network + Values | Headers are independently clickable; one evidence scrollbar handles overflow |
| Collapse Message + Network | Values gain available viewport area; entry row expanders still work |
| Receive repeated messages with one or more sections collapsed | No unexpected accordion state flip, Inspector target loss or UI-thread blocking |
| Select a historical event and return to live | Sticky context updates but accordion states persist |
| 100/125/150% Windows scaling | Circle-chevron, search, X and signal/value text remain aligned and not clipped |
| Keyboard focus and accessibility | Search and section headers can be focused; toggle activation works by keyboard |
| Import matching SCL / omit SCL | Verified names and quality when matched; raw data items with no fabricated meaning otherwise |

## CI discipline

Use one exact-SHA Windows CI and runtime/security/Inno candidate workflow set after committing the complete UX checkpoint. Review any failure and fix only the affected surface. Do not rerun successful jobs without a substantive reason. Keep v1.4.1 stable assets untouched and treat this as v1.4.2 pre-release candidate until physical acceptance.
