import { describe, expect, it } from 'vitest';
import { centerOn, clusterPoints, distanceBetween, fitView, orbitPlace, orbitRadius, screenToWorld, systemWorldSize, worldToScreen, zoomAt } from '../mapGeometry';

describe('mapGeometry', () => {
  it('fits a world into an area with a margin and centres it', () => {
    const view = fitView({ minX: 0, minY: 0, maxX: 1000, maxY: 500 }, { width: 800, height: 600 }, 20);

    expect(view.k).toBeCloseTo(0.76, 5);
    expect(worldToScreen(view, { x: 0, y: 0 }).x).toBeCloseTo(20, 5);
    expect(worldToScreen(view, { x: 1000, y: 500 }).x).toBeCloseTo(780, 5);
    expect(worldToScreen(view, { x: 500, y: 250 }).y).toBeCloseTo(300, 5);
  });

  it('goes from a pixel to the world and back', () => {
    const view = { x: 40, y: -10, k: 2.5 };
    const pixel = worldToScreen(view, { x: 120, y: 80 });

    expect(screenToWorld(view, pixel)).toEqual({ x: 120, y: 80 });
  });

  it('keeps the place under the pointer still while zooming, within the limits', () => {
    const view = { x: 10, y: 20, k: 1 };
    const pointer = { x: 300, y: 200 };
    const before = screenToWorld(view, pointer);
    const zoomed = zoomAt(view, 2, pointer, { min: 0.5, max: 8 });

    expect(zoomed.k).toBe(2);
    expect(screenToWorld(zoomed, pointer).x).toBeCloseTo(before.x, 8);
    expect(screenToWorld(zoomed, pointer).y).toBeCloseTo(before.y, 8);
    expect(zoomAt(view, 100, pointer, { min: 0.5, max: 8 }).k).toBe(8);
    expect(zoomAt(view, 0.001, pointer, { min: 0.5, max: 8 }).k).toBe(0.5);
  });

  it('puts a place in the middle of the area keeping the scale', () => {
    const view = centerOn({ x: 0, y: 0, k: 2 }, { x: 100, y: 50 }, { width: 400, height: 300 });

    expect(worldToScreen(view, { x: 100, y: 50 })).toEqual({ x: 200, y: 150 });
    expect(view.k).toBe(2);
  });

  it('groups the points of one cell, leaves the fixed ones alone and averages a group', () => {
    const points = [
      { id: 1, x: 10, y: 10 },
      { id: 2, x: 20, y: 20 },
      { id: 3, x: 500, y: 500 },
      { id: 4, x: 12, y: 12 },
    ];
    const clusters = clusterPoints(points, 100, (point) => point.id === 4);
    const group = clusters.find((cluster) => cluster.members.length > 1)!;

    expect(clusters).toHaveLength(3);
    expect(group.members.map((member) => member.id)).toEqual([1, 2]);
    expect(group).toMatchObject({ x: 15, y: 15 });
    expect(clusters.find((cluster) => cluster.members[0]?.id === 4)?.members).toHaveLength(1);
    // A cell of no size groups nothing.
    expect(clusterPoints(points, 0, () => false)).toHaveLength(4);
  });

  it('places an object on its orbit always at the same angle', () => {
    const first = orbitPlace(7, 5, 12);
    const again = orbitPlace(7, 5, 12);
    const middle = systemWorldSize / 2;

    expect(first).toEqual(again);
    expect(distanceBetween(first, { x: middle, y: middle })).toBeCloseTo(orbitRadius(5, 12), 6);
    expect(orbitRadius(12, 12)).toBeGreaterThan(orbitRadius(6, 12));
    expect(orbitPlace(8, 5, 12)).not.toEqual(first);
  });
});
