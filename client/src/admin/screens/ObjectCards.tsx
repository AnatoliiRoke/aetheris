import { useCallback, useEffect, useState, type FormEvent } from 'react';
import { Alert } from '../../components/Alert';
import type { BodyCard, FieldCard, ReferenceRow, Share, ShareInput, SystemCard, WorldReference } from '../adminApi';
import { errorKey, useAdmin } from '../adminContext';
import { AdminDialog } from '../AdminDialog';
import {
  CardField,
  FailureBanner,
  ReasonAndActions,
  SharesEditor,
  type ShareDraft,
} from '../EditParts';
import { describeFailure, parseNumber, type EditFailure } from '../editFailure';
import type { AdminMessageKey } from '../i18n/uk';
import { formatMoment, formatNumber, useAdminText, useWorldName } from '../i18n/useAdminText';
import { classLabel } from '../labels';

/** The number of decimal places of a stored coefficient, the scale of its column. */
const coefficientDecimals = 6;

/** A share of a field is typed in per cent. */
const percent = 100;

/**
 * The state of a card: loading, the data or the error of the loading.
 */
function useCard<T>(load: () => Promise<T>): {
  card: T | null;
  error: AdminMessageKey | null;
  generation: number;
  reload: () => void;
  setCard: (card: T) => void;
} {
  const { onError } = useAdmin();
  const [state, setState] = useState<{ card: T | null; error: AdminMessageKey | null }>({ card: null, error: null });
  const [version, setVersion] = useState(0);

  useEffect(() => {
    let active = true;

    load().then(
      (card) => {
        if (active) {
          setState({ card, error: null });
        }
      },
      (failure: unknown) => {
        if (active) {
          onError(failure);
          setState({ card: null, error: errorKey(failure) });
        }
      },
    );

    return () => {
      active = false;
    };
  }, [load, onError, version]);

  return {
    card: state.card,
    error: state.error,
    generation: version,
    reload: () => {
      // The form starts again from the fresh data once it arrives.
      setState({ card: null, error: null });
      setVersion((value) => value + 1);
    },
    setCard: (card: T) => setState({ card, error: null }),
  };
}

function drafts(shares: Share[], toText: (coefficient: number) => string): ShareDraft[] {
  return shares.map((share, index) => ({ key: index + 1, resource_id: share.resource_id, value: toText(Number(share.coefficient)) }));
}

/**
 * Turns the rows of the editor into the shares of the request; a row that is not a number
 * makes the whole set invalid.
 */
function toShares(rows: ShareDraft[], scale: number): ShareInput[] | null {
  const shares: ShareInput[] = [];

  for (const row of rows) {
    const value = parseNumber(row.value);

    if (value === null) {
      return null;
    }

    shares.push({ resource_id: row.resource_id, coefficient: Number((value / scale).toFixed(coefficientDecimals)) });
  }

  return shares;
}

function Loading({ error }: { error: AdminMessageKey | null }) {
  const { t } = useAdminText();

  return error ? <Alert kind="error">{t(error)}</Alert> : <p className="hint">{t('admin.loading')}</p>;
}

/**
 * The shared work of saving a card: the reason, the busy state, the refusal and the success.
 */
function useSave() {
  const { t } = useAdminText();
  const { onError } = useAdmin();
  const [reason, setReason] = useState('');
  const [busy, setBusy] = useState(false);
  const [failure, setFailure] = useState<EditFailure | null>(null);
  const [saved, setSaved] = useState(false);

  const save = async (action: () => Promise<void>, invalid: Record<string, string[]> = {}) => {
    setSaved(false);

    const fields = { ...invalid };

    if (reason.trim() === '') {
      fields['reason'] = ['required'];
    }

    if (Object.keys(fields).length > 0) {
      setFailure({ fields, conflict: false, problems: [], message: 'admin.error.validation' });
      return;
    }

    setBusy(true);
    setFailure(null);

    try {
      await action();
      setSaved(true);
      setReason('');
    } catch (error) {
      onError(error);
      setFailure(describeFailure(error));
    } finally {
      setBusy(false);
    }
  };

  const savedBanner = saved ? <Alert kind="success">{t('admin.edit.saved')}</Alert> : null;

  return { reason, setReason, busy, failure, setFailure, save, savedBanner };
}

/**
 * The card of an asteroid field (task 00009, decision 2): name, type, place, masses, the
 * depletion and the composition in per cent with the empty rock.
 */
export function FieldCardDialog({ fieldId, reference, onClose, onSaved }: { fieldId: number; reference: WorldReference | null; onClose: () => void; onSaved: () => void }) {
  const { t, language } = useAdminText();
  const { api, can } = useAdmin();
  const load = useCallback(() => api.fieldCard(fieldId), [api, fieldId]);
  const { card, error, generation, reload, setCard } = useCard<FieldCard>(load);

  return (
    <AdminDialog title={t('admin.card.fieldTitle', { name: card?.name ?? `#${fieldId}` })} onClose={onClose}>
      {card ? (
        <FieldForm
          key={generation}
          card={card}
          reference={reference}
          canSave={can('admin.world_field_updated')}
          language={language}
          onCancel={onClose}
          onReload={reload}
          onSaved={(next) => {
            setCard(next);
            onSaved();
          }}
        />
      ) : (
        <Loading error={error} />
      )}
    </AdminDialog>
  );
}

function FieldForm({
  card,
  reference,
  canSave,
  language,
  onCancel,
  onReload,
  onSaved,
}: {
  card: FieldCard;
  reference: WorldReference | null;
  canSave: boolean;
  language: 'uk' | 'en';
  onCancel: () => void;
  onReload: () => void;
  onSaved: (card: FieldCard) => void;
}) {
  const { t } = useAdminText();
  const { api } = useAdmin();
  const name = useWorldName();
  const { reason, setReason, busy, failure, save, savedBanner } = useSave();
  const [form, setForm] = useState({
    name: card.name,
    objectTypeId: card.object_type_id,
    x: String(card.x),
    y: String(card.y),
    z: String(card.z),
    mass: String(card.mass),
    initialMass: String(card.initial_mass),
    depleted: card.depleted_at !== null,
  });
  const [shares, setShares] = useState(() => drafts(card.composition_ids, (coefficient) => String(Number((coefficient * percent).toFixed(4)))));
  const types = (reference?.object_types ?? []).filter((type) => type.group_code === 'asteroid_field');
  const fixedPlace = card.system_id !== null;
  const errors = failure?.fields ?? {};

  const onSubmit = (event: FormEvent) => {
    event.preventDefault();

    const numbers = { x: parseNumber(form.x), y: parseNumber(form.y), z: parseNumber(form.z), mass: parseNumber(form.mass), initial_mass: parseNumber(form.initialMass) };
    const composition = toShares(shares, percent);
    const invalid: Record<string, string[]> = {};

    for (const [field, value] of Object.entries(numbers)) {
      if (value === null) {
        invalid[field] = ['not_a_number'];
      }
    }

    if (composition === null) {
      invalid['composition_ids'] = ['not_a_number'];
    }

    void save(async () => {
      onSaved(
        await api.updateField(card.id, {
          version: card.version,
          reason: reason.trim(),
          name: form.name,
          object_type_id: form.objectTypeId,
          x: numbers.x!,
          y: numbers.y!,
          z: numbers.z!,
          mass: numbers.mass!,
          initial_mass: numbers.initial_mass!,
          depleted: form.depleted,
          composition_ids: composition!,
        }),
      );
    }, invalid);
  };

  return (
    <form className="admin-form admin-card" onSubmit={onSubmit} noValidate>
      <p className="hint">
        {t('admin.card.fieldPlace', { sector: card.sector_id, system: card.system_name ?? t('admin.fields.free') })}
        {card.depleted_at ? ` ${t('admin.card.depletedAt', { at: formatMoment(card.depleted_at, language) })}` : null}
      </p>
      <CardField label={t('admin.col.name')} codes={errors['name']}>
        <input className="input" value={form.name} onChange={(event) => setForm({ ...form, name: event.target.value })} />
      </CardField>
      <CardField label={t('admin.col.type')} codes={errors['object_type_id']}>
        <select className="input" value={form.objectTypeId} onChange={(event) => setForm({ ...form, objectTypeId: Number(event.target.value) })}>
          {types.map((type) => (
            <option key={type.id} value={type.id} title={type.code}>
              {name(type.name_key, type.code)}
            </option>
          ))}
        </select>
      </CardField>
      <Coordinates
        values={form}
        disabled={fixedPlace}
        hint={fixedPlace ? t('admin.card.placeOfSystem') : t('admin.card.sectorBounds', { size: card.bounds.sector_size, depth: card.bounds.sector_depth })}
        errors={errors}
        onChange={(axis, value) => setForm({ ...form, [axis]: value })}
      />
      <div className="admin-form admin-form--inline">
        <CardField label={t('admin.col.mass')} codes={errors['mass']}>
          <input className="input" inputMode="numeric" value={form.mass} onChange={(event) => setForm({ ...form, mass: event.target.value })} />
        </CardField>
        <CardField label={t('admin.col.initialMass')} codes={errors['initial_mass']}>
          <input className="input" inputMode="numeric" value={form.initialMass} onChange={(event) => setForm({ ...form, initialMass: event.target.value })} />
        </CardField>
      </div>
      <label className="admin-check">
        <input type="checkbox" checked={form.depleted} onChange={(event) => setForm({ ...form, depleted: event.target.checked })} />
        <span>{t('admin.card.depleted')}</span>
      </label>
      {errors['depleted'] ? <Alert kind="error">{t('admin.edit.error.orbitTaken')}</Alert> : null}
      <SharesEditor
        rows={shares}
        onChange={setShares}
        resources={reference?.resources ?? []}
        mode="percent"
        maxShare={card.bounds.max_composition_share}
        codes={errors['composition_ids']}
      />
      <FailureBanner failure={failure} onReload={onReload} />
      {savedBanner}
      <ReasonAndActions reason={reason} onReason={setReason} codes={errors['reason']} busy={busy} canSave={canSave} onCancel={onCancel} />
    </form>
  );
}

function Coordinates({
  values,
  disabled,
  hint,
  errors,
  onChange,
}: {
  values: { x: string; y: string; z: string };
  disabled: boolean;
  hint: string;
  errors: Record<string, string[]>;
  onChange: (axis: 'x' | 'y' | 'z', value: string) => void;
}) {
  const { t } = useAdminText();

  return (
    <fieldset className="admin-card__group">
      <legend>{t('admin.card.coordinates')}</legend>
      <div className="admin-form admin-form--inline">
        {(['x', 'y', 'z'] as const).map((axis) => (
          <CardField key={axis} label={axis} codes={errors[axis]}>
            <input className="input" inputMode="decimal" value={values[axis]} disabled={disabled} onChange={(event) => onChange(axis, event.target.value)} />
          </CardField>
        ))}
      </div>
      <p className="hint">{hint}</p>
    </fieldset>
  );
}

/**
 * The card of a body (task 00009, decision 2): name, type and size, orbit and deposits as
 * richness multipliers. The owner and the name the owner gave are shown only.
 */
export function BodyCardDialog({ planetId, reference, onClose, onSaved }: { planetId: number; reference: WorldReference | null; onClose: () => void; onSaved: () => void }) {
  const { t } = useAdminText();
  const { api, can } = useAdmin();
  const load = useCallback(() => api.bodyCard(planetId), [api, planetId]);
  const { card, error, generation, reload, setCard } = useCard<BodyCard>(load);

  return (
    <AdminDialog title={t('admin.card.bodyTitle', { name: card?.owner_name ?? card?.name ?? `#${planetId}` })} onClose={onClose}>
      {card ? (
        <BodyForm
          key={generation}
          card={card}
          reference={reference}
          canSave={can('admin.world_object_updated')}
          onCancel={onClose}
          onReload={reload}
          onSaved={(next) => {
            setCard(next);
            onSaved();
          }}
        />
      ) : (
        <Loading error={error} />
      )}
    </AdminDialog>
  );
}

function BodyForm({
  card,
  reference,
  canSave,
  onCancel,
  onReload,
  onSaved,
}: {
  card: BodyCard;
  reference: WorldReference | null;
  canSave: boolean;
  onCancel: () => void;
  onReload: () => void;
  onSaved: (card: BodyCard) => void;
}) {
  const { t, language } = useAdminText();
  const { api } = useAdmin();
  const name = useWorldName();
  const { reason, setReason, busy, failure, save, savedBanner } = useSave();
  const [form, setForm] = useState({
    name: card.name,
    objectTypeId: card.object_type_id,
    sizeClassId: card.size_class_id,
    size: String(card.size),
    orbit: card.orbit ?? card.bounds.orbit_min,
  });
  const [deposits, setDeposits] = useState(() => drafts(card.deposit_ids, (coefficient) => String(coefficient)));
  const types = (reference?.object_types ?? []).filter((type) => type.group_code === card.object_class);
  const allowed = new Set(card.bounds.allowed_resource_ids);
  const resources: ReferenceRow[] = (reference?.resources ?? []).filter((resource) => allowed.has(resource.id));
  const sizeClass = card.bounds.size_class_ids.find((size) => size.id === form.sizeClassId);
  const taken = new Map(card.bounds.taken_orbit_ids.map((entry) => [entry.orbit, entry.name]));
  const orbits = Array.from({ length: card.bounds.orbit_max - card.bounds.orbit_min + 1 }, (_, index) => card.bounds.orbit_min + index);
  const errors = failure?.fields ?? {};

  const onSubmit = (event: FormEvent) => {
    event.preventDefault();

    const size = parseNumber(form.size);
    const shares = toShares(deposits, 1);
    const invalid: Record<string, string[]> = {};

    if (size === null) {
      invalid['size'] = ['not_a_number'];
    }

    if (shares === null) {
      invalid['deposit_ids'] = ['not_a_number'];
    }

    void save(async () => {
      onSaved(
        await api.updateBody(card.id, {
          version: card.version,
          reason: reason.trim(),
          name: form.name,
          object_type_id: form.objectTypeId,
          size_class_id: form.sizeClassId,
          size: size!,
          orbit: form.orbit,
          deposit_ids: shares!,
        }),
      );
    }, invalid);
  };

  return (
    <form className="admin-form admin-card" onSubmit={onSubmit} noValidate>
      <dl className="admin-card__facts">
        <dt>{t('admin.col.class')}</dt>
        <dd>{classLabel(t, card.object_class)}</dd>
        <dt>{t('admin.col.system')}</dt>
        <dd>{card.system_name}</dd>
        <dt>{t('admin.card.coordinates')}</dt>
        <dd>
          {[card.x, card.y, card.z].map((value) => formatNumber(value, language, 2)).join('; ')} <span className="hint">{t('admin.card.placeOfSystem')}</span>
        </dd>
        <dt>{t('admin.col.owner')}</dt>
        <dd>{card.owner_player_id ?? '—'}</dd>
        <dt>{t('admin.card.ownerName')}</dt>
        <dd>{card.owner_name ?? '—'}</dd>
      </dl>
      <p className="hint">{t('admin.card.ownerReadOnly')}</p>
      <CardField label={t('admin.card.generatedName')} codes={errors['name']}>
        <input className="input" value={form.name} onChange={(event) => setForm({ ...form, name: event.target.value })} />
      </CardField>
      <div className="admin-form admin-form--inline">
        <CardField label={t('admin.col.type')} codes={errors['object_type_id']}>
          <select className="input" value={form.objectTypeId} onChange={(event) => setForm({ ...form, objectTypeId: Number(event.target.value) })}>
            {types.map((type) => (
              <option key={type.id} value={type.id} title={type.code}>
                {name(type.name_key, type.code)}
              </option>
            ))}
          </select>
        </CardField>
        <CardField label={t('admin.col.sizeClass')} codes={errors['size_class_id']}>
          <select className="input" value={form.sizeClassId} onChange={(event) => setForm({ ...form, sizeClassId: Number(event.target.value) })}>
            {card.bounds.size_class_ids.map((size) => (
              <option key={size.id} value={size.id} title={size.code}>
                {name(size.name_key, size.code)}
              </option>
            ))}
          </select>
        </CardField>
        <CardField
          label={t('admin.col.size')}
          codes={errors['size']}
          hint={sizeClass ? t('admin.card.range', { min: sizeClass.size_min, max: sizeClass.size_max }) : undefined}
        >
          <input className="input" inputMode="numeric" value={form.size} onChange={(event) => setForm({ ...form, size: event.target.value })} />
        </CardField>
        <CardField label={t('admin.col.orbit')} codes={errors['orbit']}>
          <select className="input" value={form.orbit} onChange={(event) => setForm({ ...form, orbit: Number(event.target.value) })}>
            {orbits.map((orbit) => (
              <option key={orbit} value={orbit} disabled={taken.has(orbit)}>
                {taken.has(orbit) ? t('admin.card.orbitTaken', { orbit, name: taken.get(orbit)! }) : String(orbit)}
              </option>
            ))}
          </select>
        </CardField>
      </div>
      <SharesEditor rows={deposits} onChange={setDeposits} resources={resources} mode="multiplier" codes={errors['deposit_ids']} />
      <p className="hint">
        {t('admin.card.multiplierRange', {
          min: formatNumber(card.bounds.deposit_coefficient_min, language, 2),
          max: formatNumber(card.bounds.deposit_coefficient_max, language, 2),
        })}
      </p>
      <FailureBanner failure={failure} onReload={onReload} />
      {savedBanner}
      <ReasonAndActions reason={reason} onReason={setReason} codes={errors['reason']} busy={busy} canSave={canSave} onCancel={onCancel} />
    </form>
  );
}

/**
 * The card of a star system (task 00009, decision 2): name, place and star type. The objects
 * of the system move with it.
 */
export function SystemCardDialog({ systemId, reference, onClose, onSaved }: { systemId: number; reference: WorldReference | null; onClose: () => void; onSaved: () => void }) {
  const { t } = useAdminText();
  const { api, can } = useAdmin();
  const load = useCallback(() => api.systemCard(systemId), [api, systemId]);
  const { card, error, generation, reload, setCard } = useCard<SystemCard>(load);

  return (
    <AdminDialog title={t('admin.card.systemTitle', { name: card?.name ?? `#${systemId}` })} onClose={onClose}>
      {card ? (
        <SystemForm
          key={generation}
          card={card}
          reference={reference}
          canSave={can('admin.world_system_updated')}
          onCancel={onClose}
          onReload={reload}
          onSaved={(next) => {
            setCard(next);
            onSaved();
          }}
        />
      ) : (
        <Loading error={error} />
      )}
    </AdminDialog>
  );
}

function SystemForm({
  card,
  reference,
  canSave,
  onCancel,
  onReload,
  onSaved,
}: {
  card: SystemCard;
  reference: WorldReference | null;
  canSave: boolean;
  onCancel: () => void;
  onReload: () => void;
  onSaved: (card: SystemCard) => void;
}) {
  const { t } = useAdminText();
  const { api } = useAdmin();
  const name = useWorldName();
  const { reason, setReason, busy, failure, save, savedBanner } = useSave();
  const [form, setForm] = useState({ name: card.name, x: String(card.x), y: String(card.y), z: String(card.z), starTypeId: card.star_type_id ?? 0 });
  const stars = (reference?.object_types ?? []).filter((type) => type.group_code === 'star');
  const errors = failure?.fields ?? {};

  const onSubmit = (event: FormEvent) => {
    event.preventDefault();

    const numbers = { x: parseNumber(form.x), y: parseNumber(form.y), z: parseNumber(form.z) };
    const invalid: Record<string, string[]> = {};

    for (const [axis, value] of Object.entries(numbers)) {
      if (value === null) {
        invalid[axis] = ['not_a_number'];
      }
    }

    void save(async () => {
      onSaved(
        await api.updateSystem(card.id, {
          version: card.version,
          reason: reason.trim(),
          name: form.name,
          x: numbers.x!,
          y: numbers.y!,
          z: numbers.z!,
          star_type_id: form.starTypeId,
        }),
      );
    }, invalid);
  };

  return (
    <form className="admin-form admin-card" onSubmit={onSubmit} noValidate>
      <p className="hint">
        {t('admin.card.systemPlace', { sector: card.sector_name, number: card.number })} {card.in_nebula ? t('admin.card.inNebula') : null}
      </p>
      <CardField label={t('admin.col.name')} codes={errors['name']}>
        <input className="input" value={form.name} onChange={(event) => setForm({ ...form, name: event.target.value })} />
      </CardField>
      <CardField label={t('admin.col.starType')} codes={errors['star_type_id']}>
        <select className="input" value={form.starTypeId} onChange={(event) => setForm({ ...form, starTypeId: Number(event.target.value) })}>
          {stars.map((type) => (
            <option key={type.id} value={type.id} title={type.code}>
              {name(type.name_key, type.code)}
            </option>
          ))}
        </select>
      </CardField>
      <Coordinates
        values={form}
        disabled={false}
        hint={t('admin.card.sectorBounds', { size: card.bounds.sector_size, depth: card.bounds.sector_depth })}
        errors={errors}
        onChange={(axis, value) => setForm({ ...form, [axis]: value })}
      />
      <p className="hint">{t('admin.card.systemMoves')}</p>
      <FailureBanner failure={failure} onReload={onReload} />
      {savedBanner}
      <ReasonAndActions reason={reason} onReason={setReason} codes={errors['reason']} busy={busy} canSave={canSave} onCancel={onCancel} />
    </form>
  );
}
