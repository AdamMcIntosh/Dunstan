#include "JsonListModel.h"

#include <QJsonObject>
#include <QJsonValue>

int JsonListModel::rowCount(const QModelIndex &parent) const
{
    return parent.isValid() ? 0 : int(m_rows.size());
}

QVariant JsonListModel::data(const QModelIndex &index, int role) const
{
    if (!index.isValid() || index.row() >= m_rows.size())
        return {};
    const int column = role - Qt::UserRole - 1;
    if (column < 0 || column >= m_roles.size())
        return {};
    return m_rows.at(index.row()).value(m_roles.at(column)).toVariant();
}

QHash<int, QByteArray> JsonListModel::roleNames() const
{
    QHash<int, QByteArray> names;
    for (int i = 0; i < m_roles.size(); ++i)
        names.insert(Qt::UserRole + 1 + i, m_roles.at(i).toUtf8());
    return names;
}

void JsonListModel::setRoles(const QStringList &roles)
{
    if (m_roles == roles)
        return;
    beginResetModel();
    m_roles = roles;
    endResetModel();
    emit rolesChanged();
}

void JsonListModel::ensureRoles(const QJsonObject &sample)
{
    // Views read roleNames once, so roles are fixed by the QML `roles` list or the first result.
    if (m_roles.isEmpty()) {
        m_roles = sample.keys();
        emit rolesChanged();
    }
}

void JsonListModel::setJsonArray(const QJsonArray &array)
{
    beginResetModel();
    m_rows.clear();
    m_rows.reserve(array.size());
    for (const auto &value : array)
        m_rows.append(value.toObject());
    if (!m_rows.isEmpty())
        ensureRoles(m_rows.first());
    endResetModel();
    emit countChanged();
}

void JsonListModel::setItems(const QVariantList &items)
{
    setJsonArray(QJsonArray::fromVariantList(items));
}

void JsonListModel::clear()
{
    setJsonArray({});
}

void JsonListModel::setLoading(bool loading)
{
    if (m_loading == loading)
        return;
    m_loading = loading;
    emit loadingChanged();
}

void JsonListModel::setError(const QString &error)
{
    if (m_error == error)
        return;
    m_error = error;
    emit errorChanged();
}

QVariantMap JsonListModel::get(int row) const
{
    return row >= 0 && row < m_rows.size() ? m_rows.at(row).toVariantMap() : QVariantMap{};
}

int JsonListModel::indexOf(const QString &key, const QVariant &value) const
{
    const auto needle = QJsonValue::fromVariant(value);
    for (int i = 0; i < m_rows.size(); ++i) {
        const auto candidate = m_rows.at(i).value(key);
        if (candidate == needle || (candidate.isDouble() && needle.isDouble() && candidate.toDouble() == needle.toDouble()))
            return i;
    }
    return -1;
}

void JsonListModel::setProperty(int row, const QString &key, const QVariant &value)
{
    if (row < 0 || row >= m_rows.size())
        return;
    m_rows[row].insert(key, QJsonValue::fromVariant(value));
    const int column = int(m_roles.indexOf(key));
    const auto changed = index(row);
    emit dataChanged(changed, changed, column >= 0 ? QList<int>{Qt::UserRole + 1 + column} : QList<int>{});
}
