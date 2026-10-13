#pragma once

#include <QAbstractListModel>
#include <QJsonArray>
#include <QJsonObject>
#include <QtQml/qqmlregistration.h>

/// A list model whose roles are the keys of the JSON objects it holds.
/// Filled by Bridge.load() or from QML with setItems().
class JsonListModel : public QAbstractListModel
{
    Q_OBJECT
    QML_ELEMENT
    Q_PROPERTY(int count READ count NOTIFY countChanged)
    Q_PROPERTY(bool loading READ loading NOTIFY loadingChanged)
    Q_PROPERTY(QString error READ error NOTIFY errorChanged)
    Q_PROPERTY(QStringList roles READ roles WRITE setRoles NOTIFY rolesChanged)

public:
    using QAbstractListModel::QAbstractListModel;

    int rowCount(const QModelIndex &parent = {}) const override;
    QVariant data(const QModelIndex &index, int role) const override;
    QHash<int, QByteArray> roleNames() const override;

    int count() const { return int(m_rows.size()); }
    bool loading() const { return m_loading; }
    QString error() const { return m_error; }
    QStringList roles() const { return m_roles; }
    void setRoles(const QStringList &roles);

    void setJsonArray(const QJsonArray &array);
    void setLoading(bool loading);
    void setError(const QString &error);

    Q_INVOKABLE QVariantMap get(int row) const;
    Q_INVOKABLE void setItems(const QVariantList &items);
    Q_INVOKABLE void clear();
    /// Index of the first row whose `key` equals `value`, or -1.
    Q_INVOKABLE int indexOf(const QString &key, const QVariant &value) const;
    Q_INVOKABLE void setProperty(int row, const QString &key, const QVariant &value);

signals:
    void countChanged();
    void loadingChanged();
    void errorChanged();
    void rolesChanged();

private:
    void ensureRoles(const QJsonObject &sample);

    QList<QJsonObject> m_rows;
    QStringList m_roles;
    bool m_loading = false;
    QString m_error;
};
