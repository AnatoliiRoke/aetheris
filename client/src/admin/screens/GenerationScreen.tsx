import { useEffect, useState, type FormEvent } from 'react';
import { Alert } from '../../components/Alert';
import { Link } from '../../router/Router';
import type { GenerationEstimate, GenerationRun } from '../adminApi';
import { errorKey, useAdmin } from '../adminContext';
import { adminRoutes } from '../adminRoutes';
import type { AdminMessageKey } from '../i18n/uk';
import { formatMoment, useAdminText } from '../i18n/useAdminText';
import { classLabel, statusLabel } from '../labels';

type Step = { kind: 'form' } | { kind: 'confirm'; estimate: GenerationEstimate } | { kind: 'status'; runId: number };

/**
 * The generation of sectors (task 00007, decision 3): the form, the confirmation with the
 * summary, then the status of the run in the background and its result. The action cannot be
 * undone, so it is confirmed first; there is no trial run.
 */
export function GenerationScreen() {
  const { t } = useAdminText();
  const { api, can, onError } = useAdmin();
  const [step, setStep] = useState<Step>({ kind: 'form' });
  const [sectorCount, setSectorCount] = useState('1');
  const [seed, setSeed] = useState('');
  const [ruleSet, setRuleSet] = useState('current');
  const [limits, setLimits] = useState<GenerationEstimate | null>(null);
  const [error, setError] = useState<AdminMessageKey | null>(null);
  const [busy, setBusy] = useState(false);
  const allowed = can('admin.world_generation_started');

  useEffect(() => {
    let active = true;

    api.estimate(1).then(
      (answer) => {
        if (active) {
          setLimits(answer);
          setRuleSet(answer.rule_sets[0]?.code ?? 'current');
        }
      },
      (failure: unknown) => {
        onError(failure);
        if (active) {
          setError(errorKey(failure));
        }
      },
    );

    return () => {
      active = false;
    };
  }, [api, onError]);

  const onReview = async (event: FormEvent) => {
    event.preventDefault();
    setError(null);
    setBusy(true);

    try {
      const estimate = await api.estimate(Number(sectorCount));
      setLimits(estimate);

      if (!estimate.allowed) {
        setError(estimate.active_run_id ? 'admin.error.generationRunning' : 'admin.error.generationTooLarge');
        return;
      }

      setStep({ kind: 'confirm', estimate });
    } catch (failure) {
      onError(failure);
      setError(errorKey(failure));
    } finally {
      setBusy(false);
    }
  };

  const onConfirm = async () => {
    setError(null);
    setBusy(true);

    try {
      const run = await api.startGeneration(Number(sectorCount), seed.trim() === '' ? null : Number(seed), ruleSet);
      setStep({ kind: 'status', runId: run.id });
    } catch (failure) {
      onError(failure);
      setError(errorKey(failure));
      setStep({ kind: 'form' });
    } finally {
      setBusy(false);
    }
  };

  return (
    <>
      <header className="admin-screen__head">
        <h1 className="admin-screen__title">{t('admin.generation.title')}</h1>
        <p className="hint">{t('admin.generation.lead')}</p>
      </header>

      {error ? <Alert kind="error">{t(error)}</Alert> : null}

      {step.kind === 'form' ? (
        <form className="panel admin-form" onSubmit={(event) => void onReview(event)}>
          {limits ? (
            <p className="hint">
              {t('admin.generation.limits', {
                sectors: limits.max_sectors_per_run,
                systems: limits.max_systems_per_run,
                min: limits.systems_min,
                max: limits.systems_max,
              })}
            </p>
          ) : null}
          {limits?.active_run_id ? (
            <Alert kind="info">
              {t('admin.generation.active', { id: limits.active_run_id })}{' '}
              <button className="text-btn" type="button" onClick={() => setStep({ kind: 'status', runId: limits.active_run_id! })}>
                {t('admin.generation.showStatus')}
              </button>
            </Alert>
          ) : null}
          <label className="admin-field">
            <span>{t('admin.generation.sectorCount')}</span>
            <input
              className="input"
              type="number"
              min={1}
              max={limits?.max_sectors_per_run}
              required
              value={sectorCount}
              onChange={(event) => setSectorCount(event.target.value)}
            />
          </label>
          <label className="admin-field">
            <span>{t('admin.generation.seed')}</span>
            <input
              className="input"
              type="number"
              min={0}
              step={1}
              value={seed}
              placeholder={t('admin.generation.seedHint')}
              onChange={(event) => setSeed(event.target.value)}
            />
          </label>
          <label className="admin-field">
            <span>{t('admin.generation.ruleSet')}</span>
            <select className="input" value={ruleSet} onChange={(event) => setRuleSet(event.target.value)}>
              {(limits?.rule_sets ?? []).map((set) => (
                <option key={set.code} value={set.code}>
                  {t('admin.generation.ruleSetOption', { code: set.code, groups: set.rule_group_count, rules: set.rule_count })}
                </option>
              ))}
            </select>
          </label>
          <div className="admin-form__actions">
            <button className="btn btn--primary" type="submit" disabled={!allowed || busy}>
              {t('admin.generation.review')}
            </button>
            {!allowed ? <span className="hint">{t('admin.error.accessDenied')}</span> : null}
          </div>
        </form>
      ) : null}

      {step.kind === 'confirm' ? (
        <section className="panel admin-form" aria-labelledby="generation-confirm">
          <h2 className="panel__title" id="generation-confirm">
            {t('admin.generation.confirmTitle')}
          </h2>
          <p>{t('admin.generation.confirmText', { sectors: step.estimate.sector_count, systems: step.estimate.estimated_system_count })}</p>
          <dl className="facts">
            <dt>{t('admin.generation.seed')}</dt>
            <dd>{seed.trim() === '' ? t('admin.generation.seedDerived') : seed}</dd>
            <dt>{t('admin.generation.ruleSet')}</dt>
            <dd>{ruleSet}</dd>
          </dl>
          <Alert kind="info">{t('admin.generation.irreversible')}</Alert>
          <div className="admin-form__actions">
            <button className="btn btn--primary" type="button" disabled={busy} onClick={() => void onConfirm()}>
              {t('admin.generation.start')}
            </button>
            <button className="btn btn--secondary" type="button" disabled={busy} onClick={() => setStep({ kind: 'form' })}>
              {t('admin.generation.back')}
            </button>
          </div>
        </section>
      ) : null}

      {step.kind === 'status' ? (
        <RunStatus runId={step.runId} pollSeconds={limits?.status_poll_seconds ?? 1} onNew={() => setStep({ kind: 'form' })} />
      ) : null}
    </>
  );
}

/**
 * The status of a run: asked again while the run is queued or running, then its summary.
 */
function RunStatus({ runId, pollSeconds, onNew }: { runId: number; pollSeconds: number; onNew: () => void }) {
  const { t, language } = useAdminText();
  const { api, onError } = useAdmin();
  const [run, setRun] = useState<GenerationRun | null>(null);
  const [error, setError] = useState<AdminMessageKey | null>(null);

  useEffect(() => {
    let active = true;
    let timer: ReturnType<typeof setTimeout> | undefined;

    const load = () => {
      api.run(runId).then(
        (answer) => {
          if (!active) {
            return;
          }

          setRun(answer);

          if (answer.status === 'queued' || answer.status === 'running') {
            timer = setTimeout(load, pollSeconds * 1000);
          }
        },
        (failure: unknown) => {
          onError(failure);
          if (active) {
            setError(errorKey(failure));
          }
        },
      );
    };

    load();

    return () => {
      active = false;
      clearTimeout(timer);
    };
  }, [api, runId, pollSeconds, onError]);

  if (error) {
    return <Alert kind="error">{t(error)}</Alert>;
  }

  if (!run) {
    return <p role="status">{t('admin.loading')}</p>;
  }

  const finished = run.status === 'succeeded' || run.status === 'failed';

  return (
    <section className="panel admin-form" aria-labelledby="generation-status" aria-live="polite">
      <h2 className="panel__title" id="generation-status">
        {t('admin.generation.statusTitle', { id: run.id })}
      </h2>
      <p className={`admin-status admin-status--${run.status}`}>{statusLabel(t, run.status)}</p>
      <dl className="facts">
        <dt>{t('admin.col.createdAt')}</dt>
        <dd>{formatMoment(run.created_at, language)}</dd>
        <dt>{t('admin.col.finishedAt')}</dt>
        <dd>{formatMoment(run.finished_at, language)}</dd>
        <dt>{t('admin.generation.sectorCount')}</dt>
        <dd>{run.sector_count}</dd>
        <dt>{t('admin.generation.seed')}</dt>
        <dd>{run.seed ?? t('admin.generation.seedDerived')}</dd>
      </dl>
      {finished ? (
        <>
          <h3>{t('admin.generation.summary')}</h3>
          <dl className="facts">
            <dt>{t('admin.col.sectors')}</dt>
            <dd>{run.summary.sectors.map((sector) => `#${sector.sector_id} (${sector.seed})`).join(', ') || '—'}</dd>
            <dt>{t('admin.col.systems')}</dt>
            <dd>{run.summary.system_count}</dd>
            {Object.entries(run.summary.object_counts_by_class).map(([objectClass, count]) => (
              <SummaryRow key={objectClass} label={classLabel(t, objectClass)} value={count} />
            ))}
          </dl>
          {run.error ? <Alert kind="error">{t('admin.generation.failedWith', { code: run.error })}</Alert> : null}
          <div className="admin-form__actions">
            <Link className="btn btn--secondary" to={adminRoutes.sectors}>
              {t('admin.nav.sectors')}
            </Link>
            <button className="btn btn--primary" type="button" onClick={onNew}>
              {t('admin.generation.newRun')}
            </button>
          </div>
        </>
      ) : null}
    </section>
  );
}

function SummaryRow({ label, value }: { label: string; value: number }) {
  return (
    <>
      <dt>{label}</dt>
      <dd>{value}</dd>
    </>
  );
}
