import { useEffect, useState, type FormEvent } from 'react';
import { Alert } from '../../components/Alert';
import { useTexts } from '../../texts/useTexts';
import type { TextsByLanguage, WorldReference } from '../adminApi';
import { useAdmin } from '../adminContext';
import { CardField, FailureBanner, ReasonAndActions, WorldName } from '../EditParts';
import { describeFailure, type EditFailure } from '../editFailure';
import { useAdminText, useWorldName } from '../i18n/useAdminText';
import { useTextSettings } from '../useTextSettings';

/**
 * The resources of the world and the creation of a new one (task 00022, decision 6): the server
 * gives the neutral code, the administrator gives the group and the name and description in
 * every language of the texts. The new resource is data: no release is needed.
 */
export function ResourcesScreen() {
  const { t } = useAdminText();
  const { api, can, onError } = useAdmin();
  const { text } = useTexts();
  const [reference, setReference] = useState<WorldReference | null>(null);
  const [version, setVersion] = useState(0);

  useEffect(() => {
    let active = true;

    api.reference().then(
      (answer) => {
        if (active) {
          setReference(answer);
        }
      },
      (failure: unknown) => onError(failure),
    );

    return () => {
      active = false;
    };
  }, [api, onError, version]);

  const groups = new Map((reference?.resource_groups ?? []).map((group) => [group.id, group]));

  return (
    <>
      <header className="admin-screen__head">
        <h1 className="admin-screen__title">{t('admin.resources.title')}</h1>
        <p className="hint">{t('admin.resources.lead')}</p>
      </header>
      {can('admin.world_reference_row_added') && reference ? (
        <ResourceForm reference={reference} onCreated={() => setVersion((value) => value + 1)} />
      ) : null}
      {reference ? (
        <div className="admin-table">
          <div className="admin-table__scroll">
            <table>
              <caption className="visually-hidden">{t('admin.resources.title')}</caption>
              <thead>
                <tr>
                  <th scope="col">{t('admin.col.id')}</th>
                  <th scope="col">{t('admin.col.name')}</th>
                  <th scope="col">{t('admin.col.group')}</th>
                  <th scope="col">{t('admin.col.description')}</th>
                  <th scope="col">{t('admin.col.code')}</th>
                </tr>
              </thead>
              <tbody>
                {reference.resources.map((resource) => {
                  const group = resource.group_id ? groups.get(resource.group_id) : undefined;
  
                  return (
                    <tr key={resource.id}>
                      <td className="num">{resource.id}</td>
                      <td>
                        <WorldName nameKey={resource.name_key} code={resource.code} />
                      </td>
                      <td>{group ? <WorldName nameKey={group.name_key} code={group.code} /> : '—'}</td>
                      <td>{text(resource.description_key) ?? '—'}</td>
                      <td>
                        <code>{resource.code}</code>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </div>
      ) : (
        <p className="hint" role="status">
          {t('admin.loading')}
        </p>
      )}
    </>
  );
}

function ResourceForm({ reference, onCreated }: { reference: WorldReference; onCreated: () => void }) {
  const { t } = useAdminText();
  const { api, onError } = useAdmin();
  const { reload } = useTexts();
  const name = useWorldName();
  const settings = useTextSettings();
  const groups = reference.resource_groups ?? [];
  const [open, setOpen] = useState(false);
  const [groupId, setGroupId] = useState<number | null>(null);
  const [names, setNames] = useState<TextsByLanguage>({});
  const [descriptions, setDescriptions] = useState<TextsByLanguage>({});
  const [reason, setReason] = useState('');
  const [failure, setFailure] = useState<EditFailure | null>(null);
  const [created, setCreated] = useState(false);
  const [busy, setBusy] = useState(false);
  const languages = settings?.languages ?? [];
  const chosenGroup = groupId ?? groups[0]?.id ?? null;

  const reset = () => {
    setNames({});
    setDescriptions({});
    setReason('');
  };

  const onSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setFailure(null);
    setCreated(false);

    if (chosenGroup === null) {
      return;
    }

    setBusy(true);

    try {
      await api.createResource({ resource_group_id: chosenGroup, name: names, description: descriptions, reason: reason.trim() });
      setCreated(true);
      reset();
      reload();
      onCreated();
    } catch (error) {
      onError(error);
      setFailure(describeFailure(error));
    } finally {
      setBusy(false);
    }
  };

  if (!open) {
    return (
      <div className="admin-form__actions">
        <button className="btn btn--primary" type="button" onClick={() => setOpen(true)}>
          {t('admin.resources.create')}
        </button>
        {created ? <Alert kind="success">{t('admin.resources.created')}</Alert> : null}
      </div>
    );
  }

  return (
    <form className="admin-form admin-card panel" onSubmit={(event) => void onSubmit(event)} noValidate aria-label={t('admin.resources.create')}>
      <h2 className="admin-screen__title">{t('admin.resources.create')}</h2>
      <p className="hint">{t('admin.resources.codeHint')}</p>
      {groups.length === 0 ? <Alert kind="info">{t('admin.resources.noGroups')}</Alert> : null}
      <CardField label={t('admin.col.group')} codes={failure?.fields['resource_group_id']}>
        <select className="input" value={chosenGroup ?? ''} onChange={(event) => setGroupId(Number(event.target.value))}>
          {groups.map((group) => (
            <option key={group.id} value={group.id} title={group.code}>
              {name(group.name_key, group.code)}
            </option>
          ))}
        </select>
      </CardField>
      {languages.map((language) => (
        <div className="admin-form admin-form--inline" key={language}>
          <CardField label={t('admin.resources.name', { language })} codes={failure?.fields[`name.${language}`]}>
            <input
              className="input"
              maxLength={settings?.max_length}
              value={names[language] ?? ''}
              onChange={(event) => setNames({ ...names, [language]: event.target.value })}
            />
          </CardField>
          <CardField label={t('admin.resources.description', { language })} codes={failure?.fields[`description.${language}`]}>
            <input
              className="input"
              maxLength={settings?.max_length}
              value={descriptions[language] ?? ''}
              onChange={(event) => setDescriptions({ ...descriptions, [language]: event.target.value })}
            />
          </CardField>
        </div>
      ))}
      <FailureBanner failure={failure} onReload={onCreated} />
      {created ? <Alert kind="success">{t('admin.resources.created')}</Alert> : null}
      <ReasonAndActions
        reason={reason}
        onReason={setReason}
        codes={failure?.fields['reason']}
        busy={busy}
        canSave={chosenGroup !== null}
        onCancel={() => {
          reset();
          setFailure(null);
          setOpen(false);
        }}
      />
    </form>
  );
}
