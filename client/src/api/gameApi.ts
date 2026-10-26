import { toCivilization, type Civilization, type CivilizationAnswer } from './authApi';
import type { HttpClient } from './http';

/**
 * A race the player can choose at the first step (task 00006, decision 9).
 */
export interface Race {
  id: number;
  code: string;
  nameKey: string;
}

/**
 * The world endpoints the first step of the game needs (`/api/v1/world`).
 */
export interface GameApi {
  races(): Promise<Race[]>;
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
      const answer = await http.get<{ id: number; code: string; name_key: string }[]>(`${prefix}/races`);
      return answer.map((race) => ({ id: race.id, code: race.code, nameKey: race.name_key }));
    },
    async foundCivilization(raceId, planetName) {
      return toCivilization(await http.command<CivilizationAnswer>(`${prefix}/civilization`, { race_id: raceId, planet_name: planetName }));
    },
  };
}
