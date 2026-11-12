#pragma once
#include "PropertySource.h"

namespace rst {
// Opt-in typed payloads for Workbench's existing SetVariableValueCommand.
// Installation is process-resident. Unrelated commands keep their native owner.
class NativeArrayCommands final
{
public:
    static QString install();
    static bool available();
    // A preparation owns its strings. No borrowed source pointer is retained.
    static QString prepare(const QString &receipt, const PropertyDestination &destination,
        const QString &replacement);
    static QString wireValue(const QString &receipt, bool rollback = false);
    static QString outcome(const QString &receipt, bool rollback = false);
    // Only after a dispatched write's outcome is known. Adopted command payloads
    // remain alive until Workbench destroys those commands.
    static void release(const QString &receipt);
};
}
