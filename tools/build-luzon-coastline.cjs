'use strict';

const fs = require('node:fs');
const path = require('node:path');

const source = process.argv[2] || path.resolve(__dirname, '..', '..', 'gcbh_dynamic_campaign', 'mission-map', 'renderer', 'data', 'global-land.geojson');
const output = path.resolve(__dirname, '..', 'SeaOfUncertainty', 'Assets', 'Resources', 'Geography', 'luzon-strait-coastline.json');
const bounds = { west: 118.5, east: 125.8, south: 17.4, north: 24.1 };

function clip(ring, inside, intersect) {
  const result = [];
  for (let i = 0; i < ring.length; i += 1) {
    const current = ring[i];
    const previous = ring[(i + ring.length - 1) % ring.length];
    const currentInside = inside(current), previousInside = inside(previous);
    if (currentInside) {
      if (!previousInside) result.push(intersect(previous, current));
      result.push(current);
    } else if (previousInside) result.push(intersect(previous, current));
  }
  return result;
}

function clipBounds(input) {
  let ring = input.slice(0, input.length > 1 && input[0][0] === input[input.length - 1][0] && input[0][1] === input[input.length - 1][1] ? -1 : undefined);
  ring = clip(ring, ([x]) => x >= bounds.west, (a, b) => [bounds.west, a[1] + (b[1] - a[1]) * (bounds.west - a[0]) / (b[0] - a[0])]);
  ring = clip(ring, ([x]) => x <= bounds.east, (a, b) => [bounds.east, a[1] + (b[1] - a[1]) * (bounds.east - a[0]) / (b[0] - a[0])]);
  ring = clip(ring, ([, y]) => y >= bounds.south, (a, b) => [a[0] + (b[0] - a[0]) * (bounds.south - a[1]) / (b[1] - a[1]), bounds.south]);
  ring = clip(ring, ([, y]) => y <= bounds.north, (a, b) => [a[0] + (b[0] - a[0]) * (bounds.north - a[1]) / (b[1] - a[1]), bounds.north]);
  return ring;
}

function perpendicular(point, start, end) {
  const dx = end[0] - start[0], dy = end[1] - start[1];
  if (dx === 0 && dy === 0) return Math.hypot(point[0] - start[0], point[1] - start[1]);
  const t = Math.max(0, Math.min(1, ((point[0] - start[0]) * dx + (point[1] - start[1]) * dy) / (dx * dx + dy * dy)));
  return Math.hypot(point[0] - (start[0] + t * dx), point[1] - (start[1] + t * dy));
}

function simplify(points, tolerance) {
  if (points.length <= 3) return points;
  let farthest = 0, index = 0;
  for (let i = 1; i < points.length - 1; i += 1) {
    const distance = perpendicular(points[i], points[0], points[points.length - 1]);
    if (distance > farthest) { farthest = distance; index = i; }
  }
  if (farthest <= tolerance) return [points[0], points[points.length - 1]];
  return [...simplify(points.slice(0, index + 1), tolerance).slice(0, -1), ...simplify(points.slice(index), tolerance)];
}

const geojson = JSON.parse(fs.readFileSync(source, 'utf8'));
const polygons = [];
for (const feature of geojson.features || []) {
  const sets = feature.geometry?.type === 'Polygon' ? [feature.geometry.coordinates] : feature.geometry?.type === 'MultiPolygon' ? feature.geometry.coordinates : [];
  for (const rings of sets) {
    const outer = rings?.[0];
    if (!outer?.length) continue;
    const polygonBounds = outer.reduce((value, [x, y]) => ({ west: Math.min(value.west, x), east: Math.max(value.east, x), south: Math.min(value.south, y), north: Math.max(value.north, y) }), { west: Infinity, east: -Infinity, south: Infinity, north: -Infinity });
    if (polygonBounds.east < bounds.west || polygonBounds.west > bounds.east || polygonBounds.north < bounds.south || polygonBounds.south > bounds.north) continue;
    let clipped = clipBounds(outer);
    if (clipped.length < 3) continue;
    clipped = simplify([...clipped, clipped[0]], 0.004).slice(0, -1);
    if (clipped.length < 3) continue;
    polygons.push({ points: clipped.map(([longitude, latitude]) => ({ longitude, latitude })) });
  }
}

const result = {
  metadata: {
    title: 'Luzon Strait Natural Earth 1:10m coastline crop',
    source: 'Local GCBH cached Natural Earth 1:10m land and minor islands',
    sourceFile: source,
    license: 'Natural Earth public domain',
  },
  ...bounds,
  polygons,
};
fs.mkdirSync(path.dirname(output), { recursive: true });
fs.writeFileSync(output, `${JSON.stringify(result)}\n`, 'utf8');
console.log(`Wrote ${polygons.length} coastline polygons to ${output} (${fs.statSync(output).size} bytes).`);
