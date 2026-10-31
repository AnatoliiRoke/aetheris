import { useState } from 'react';
import type { RatePoint } from '../../../api/marketApi';
import { useI18n } from '../../../i18n/useI18n';
import { formatDecimal } from './marketFormat';

const width = 560;
const height = 230;
const left = 52;
const right = 544;
const top = 16;
const bottom = 186;
const gridLines = 5;
const paddingShare = 0.25;
const emptyRangeShare = 0.1;
const tooltipMin = 14;
const tooltipMax = 86;
const percentOfWhole = 100;

/**
 * The chart of the rate of the exchange over the last days: a line with an area under it, five
 * lines of the grid with their values and the day under each point. The tooltip shows the rate of the
 * day under the pointer or the focus. The values are the trade currency for one credit.
 *
 * @param points The rate at the end of every day, oldest first, as credits for one unit of the trade currency.
 */
export function RateChart({ points }: { points: RatePoint[] }) {
  const { t, language } = useI18n();
  const [hover, setHover] = useState(-1);

  if (points.length === 0) {
    return <p className="mk-chart__note">{t('market.chartEmpty')}</p>;
  }

  const values = points.map((point) => 1 / point.rate);
  const low = Math.min(...values);
  const high = Math.max(...values);
  const span = high - low > 0 ? high - low : high * emptyRangeShare || 1;
  const min = low - span * paddingShare;
  const max = high + span * paddingShare;
  const x = (index: number) => (points.length === 1 ? (left + right) / 2 : left + (index * (right - left)) / (points.length - 1));
  const y = (value: number) => top + ((max - value) / (max - min)) * (bottom - top);
  const grid = Array.from({ length: gridLines }, (_, index) => min + ((max - min) * index) / (gridLines - 1));
  const line = values.map((value, index) => `${index === 0 ? 'M' : 'L'}${x(index)} ${y(value)}`).join(' ');
  const area = `${line} L${x(values.length - 1)} ${bottom} L${x(0)} ${bottom} Z`;
  const locale = language === 'uk' ? 'uk-UA' : 'en-GB';
  const dayName = (point: RatePoint) => {
    const name = new Date(point.day).toLocaleDateString(locale, { weekday: 'short', timeZone: 'UTC' });

    return name.charAt(0).toUpperCase() + name.slice(1);
  };
  const points2 = points.map((point, index) => ({ point, value: values[index], index }));
  const active = hover >= 0 ? points2[hover] : null;
  const tipLeft = active ? Math.min(tooltipMax, Math.max(tooltipMin, (x(active.index) / width) * percentOfWhole)) : 0;
  const aria = t('market.chartAria', { points: points2.map(({ point, value }) => `${dayName(point)} ${formatDecimal(value, language, 2)}`).join(', ') });

  return (
    <div className="mk-chart">
      <svg aria-label={aria} className="mk-chart__svg" role="img" viewBox={`0 0 ${width} ${height}`}>
        {grid.map((value) => (
          <line className="mk-chart__grid" key={value} x1={left} x2={right} y1={y(value)} y2={y(value)} />
        ))}
        {values.length > 1 ? <path className="mk-chart__area" d={area} /> : null}
        {values.length > 1 ? <path className="mk-chart__line" d={line} /> : null}
        {points2.map(({ value, index }) => (
          <g key={index}>
            <circle className="mk-chart__dot" cx={x(index)} cy={y(value)} r={hover === index ? 6.5 : 4.5} />
            <rect
              className="mk-chart__hit"
              height={bottom - top}
              onBlur={() => setHover(-1)}
              onClick={() => setHover(index)}
              onFocus={() => setHover(index)}
              onMouseEnter={() => setHover(index)}
              onMouseLeave={() => setHover(-1)}
              tabIndex={0}
              width={points.length === 1 ? 80 : (right - left) / (points.length - 1)}
              x={x(index) - (points.length === 1 ? 40 : (right - left) / (points.length - 1) / 2)}
              y={top}
            />
          </g>
        ))}
      </svg>
      {grid.map((value) => (
        <span aria-hidden="true" className="mk-chart__ylabel" key={value} style={{ top: `${(y(value) / height) * percentOfWhole}%` }}>
          {formatDecimal(value, language, 2)}
        </span>
      ))}
      {points2.map(({ point, index }) => (
        <span aria-hidden="true" className="mk-chart__xlabel" key={index} style={{ left: `${(x(index) / width) * percentOfWhole}%` }}>
          {dayName(point)}
        </span>
      ))}
      <span aria-hidden="true" className="mk-chart__unit">
        {t('market.yAxis')}
      </span>
      {active ? (
        <div className="mk-chart__tip" role="status" style={{ left: `${tipLeft}%`, top: `${(y(active.value) / height) * percentOfWhole}%` }}>
          {t('market.chartPoint', { day: dayName(active.point), rate: formatDecimal(active.value, language, 2), unit: t('market.yAxis') })}
        </div>
      ) : null}
    </div>
  );
}
