import { images } from '../assets/images';

interface LogoMarkProps {
  lazy?: boolean;
}

/**
 * The logo mark and the word mark, without a link.
 */
export function LogoContent({ lazy = false }: LogoMarkProps) {
  return (
    <>
      <img className="logo__mark" src={images.logoMark} alt="" width={32} height={32} loading={lazy ? 'lazy' : undefined} />
      <span className="logo__text">AETHERIS</span>
    </>
  );
}
