import { toCivilization, type Civilization, type CivilizationAnswer } from './authApi';
import type { HttpClient } from './http';

/**
 * A race the player can choose at the first step (task 00006, decision 9). Its texts, picture
 * and colour come from the data of the server (task 00022); the client knows no race by name.
 */
export interface Race {
  id: number;
  nameKey: string;
  descriptionKey: string;
  moodKey: string;
  traitKeys: string[];
  imageKey: string | null;
  colorKey: string | null;
}

interface RaceAnswer {
  id: number;
  name_key: string;
  description_key: string;
  mood_key: string;
  trait_keys: string[];
  image_key: string | null;
  color_key: string | null;
}

function toRace(answer: RaceAnswer): Race {
  return {
    id: answer.id,
    nameKey: answer.name_key,
    descriptionKey: answer.description_key,
    moodKey: answer.mood_key,
    traitKeys: answer.trait_keys,
    imageKey: answer.image_key,
    colorKey: answer.color_key,
  };
}

/**
 * The world endpoints the first step of the game needs (`/api/v1/world`).
 */
export interface GameApi {
  races(): Promise<Race[]>;
  /** The races for the landing page, without a sign-in. */
  publicRaces(): Promise<Race[]>;
  /** Creates the civilization: the race and the name of the home planet. */
  foundCivilization(raceId: number, planetName: string): Promise<Civilization>;
}

const prefix = '/api/v1/world';

/**
 * The world endpoints over HTTP.
 */
export function createGameApi(http: HttpClient): GameApi {
  return {
    async races() {
      return (await http.get<RaceAnswer[]>(`${prefix}/races`)).map(toRace);
    },
    async publicRaces() {
      return (await http.get<RaceAnswer[]>('/api/v1/public/races')).map(toRace);
    },
    async foundCivilization(raceId, planetName) {
      return toCivilization(await http.command<CivilizationAnswer>(`${prefix}/civilization`, { race_id: raceId, planet_name: planetName }));
    },
  };
}
