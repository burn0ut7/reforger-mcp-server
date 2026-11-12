#pragma once
#include <QObject>
#include <QString>
#include <QStringList>
#include <QVector>

namespace rst {
struct PropertyPathEntry {
    QString key;
    int index = -1;
    QString className;
};
struct PropertyDestination {
    QString state;
    // Process-local binding comparison only; never sent as a script identity.
    QByteArray bindingFingerprint;
    QByteArray leafIdentity;
    // Canonical hexadecimal EntityID, never a native pointer.
    QString entityIdHex;
    QString key;
    QString className;
    QVector<PropertyPathEntry> path;
    int arrayIndex = -1;
    // Complete ordered snapshot: validating only the chosen element misses
    // edits/reordering elsewhere in the same array during preparation.
    QStringList arrayValues;
};
struct PropertySourceValue {
    QString state;
    QString serializedValue;
    int arrayIndex = -1;
};

// Read the source represented by one current property row, not the widget's
// draft. Caller supplies the image only after exact Compatibility validation.
// No pointers escape the synchronous application-thread call. This does not
// establish that every target of a multiple-selection edit was updated.
PropertySourceValue readPropertySource(QObject *property, quintptr validatedImage,
    PropertyDestination *destination = nullptr);
}
