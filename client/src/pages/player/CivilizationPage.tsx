import { useEffect, useRef, useState, type FormEvent } from 'react';
import type { Race } from '../../api/gameApi';
import { useApi } from '../../api/useApi';
import { Alert } from '../../components/Alert';
import { TextField } from '../../components/FormField';
import { useDocumentTitle } from '../../components/useDocumentTitle';
import { siteConfig } from '../../config/site';
import { useI18n } from '../../i18n/useI18n';
import type { MessageKey } from '../../i18n/uk';
import { routes } from '../../router/context';
import { useRouter } from '../../router/useRouter';
import { useSession } from '../../session/useSession';
import { useTexts } from '../../texts/useTexts';
import { describeError, fieldRuleErrors, planetNameProblems, type ErrorMessage } from '../auth/authErrors';
import { PlayerLayout } from './PlayerLayout';

/**
 * The first step of the game (task 00006, decision 9): choose one of the races and name the
 * home planet. Only then the server gives the start planet; the race does not change later.
 */
export function CivilizationPage() {
  const { t } = useI18n();
  const { name, text } = useTexts();
  const { game } = useApi();
  const { refresh } = useSession();
  const { navigate } = useRouter();
  useDocumentTitle(t('meta.civilization.title'));

  const [races, setRaces] = useState<Race[] | null>(null);
  const [raceId, setRaceId] = useState<number | null>(null);
  const [planetName, setPlanetName] = useState('');
  const [raceError, setRaceError] = useState(false);
  const [nameErrors, setNameErrors] = useState<MessageKey[]>([]);
  const [failure, setFailure] = useState<ErrorMessage | null>(null);
  const [busy, setBusy] = useState(false);
  const nameRef = useRef<HTMLInputElement | null>(null);
  const params = { min: siteConfig.planetNameMinLength, max: siteConfig.planetNameMaxLength };

  useEffect(() => {
    let current = true;

    game.races().then(
      (list) => current && setRaces(list),
      (error: unknown) => current && setFailure(describeError(error)),
    );

    return () => {
      current = false;
    };
  }, [game]);

  const onSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setFailure(null);

    const problems = planetNameProblems(planetName);
    setRaceError(raceId === null);
    setNameErrors(problems);

    if (raceId === null || problems.length > 0) {
      if (problems.length > 0) {
        nameRef.current?.focus();
      }

      return;
    }

    setBusy(true);

    try {
      await game.foundCivilization(raceId, planetName.trim());
      await refresh();
      navigate(routes.game, { replace: true });
    } catch (error) {
      const fields = fieldRuleErrors(error, 'planet_name');

      if (fields.length > 0) {
        setNameErrors(fields);
        nameRef.current?.focus();
      } else {
        setFailure(describeError(error));
      }
    } finally {
      setBusy(false);
    }
  };

  return (
    <PlayerLayout showNavigation={false}>
      <div>
        <h1 className="player__title">{t('player.civilization.title')}</h1>
        <p className="lead">{t('player.civilization.lead')}</p>
      </div>

      {failure ? <Alert kind="error" title={t(failure.title)}>{t(failure.text)}</Alert> : null}

      <form className="form panel" noValidate onSubmit={onSubmit}>
        <fieldset className="race-grid" aria-describedby={raceError ? 'race-error' : undefined}>
          <legend className="field__label">{t('player.civilization.race')}</legend>
          {races === null ? <p role="status">{t('common.loading')}</p> : null}
          {races?.map((race) => (
            <label key={race.id} className="race-option">
              <input type="radio" name="race" value={race.id} checked={raceId === race.id} onChange={() => { setRaceId(race.id); setRaceError(false); }} />
              <span className="race-option__name">{name(race.nameKey)}</span>
              <span className="race-option__text">{text(race.descriptionKey) ?? ''}</span>
            </label>
          ))}
        </fieldset>
        {raceError ? (
          <p className="error-text" id="race-error">
            {t('error.raceRequired')}
          </p>
        ) : null}
        <TextField
          ref={nameRef}
          label={t('player.civilization.planetName')}
          name="planet-name"
          placeholder={t('player.civilization.planetPlaceholder')}
          maxLength={siteConfig.planetNameMaxLength}
          required
          value={planetName}
          hint={t('player.civilization.planetHint', params)}
          error={nameErrors.length > 0 ? nameErrors.map((key) => t(key, params)).join(' ') : undefined}
          onChange={(event) => {
            setPlanetName(event.target.value);
            setNameErrors([]);
          }}
        />
        <p className="hint">{t('player.civilization.final')}</p>
        <button className="btn btn--primary btn--lg btn--block" type="submit" disabled={busy || races === null} aria-busy={busy}>
          {t('player.civilization.submit')}
        </button>
      </form>
    </PlayerLayout>
  );
}
