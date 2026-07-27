// @ts-check
import {themes as prismThemes} from 'prism-react-renderer';

/** @type {import('@docusaurus/types').Config} */
const config = {
  title: 'SubZeroDev.WinGet',
  tagline: 'A C# client library for the WinGet COM API',
  favicon: 'img/favicon.ico',

  future: {
    v4: true, // Improve compatibility with the upcoming Docusaurus v4
  },

  // Served from a dedicated custom domain (winget.subzerodev.com — see
  // website/static/CNAME), so this serves from the root, not a repo-name subpath.
  url: 'https://winget.subzerodev.com',
  baseUrl: '/',

  organizationName: 'The-Running-Dev',
  projectName: 'SubZeroDev.WinGet',

  // The build IS the link checker: any unresolved internal link fails CI.
  onBrokenLinks: 'throw',
  markdown: {
    hooks: {
      onBrokenMarkdownLinks: 'throw',
    },
  },

  i18n: {
    defaultLocale: 'en',
    locales: ['en'],
  },

  presets: [
    [
      'classic',
      /** @type {import('@docusaurus/preset-classic').Options} */
      ({
        // Content lives in the repo root's docs/ (the same tree used for the
        // in-repo GitHub-browsable Markdown) so there is exactly one source
        // of truth for the documentation.
        docs: {
          path: '../docs',
          routeBasePath: '/',
          sidebarPath: './sidebars.js',
          editUrl: 'https://github.com/The-Running-Dev/SubZeroDev.WinGet/edit/main/docs/',
        },
        blog: false,
        theme: {
          customCss: './src/css/custom.css',
        },
      }),
    ],
  ],

  themeConfig:
    /** @type {import('@docusaurus/preset-classic').ThemeConfig} */
    ({
      colorMode: {
        respectPrefersColorScheme: true,
      },
      navbar: {
        title: 'SubZeroDev.WinGet',
        items: [
          {
            href: 'https://github.com/The-Running-Dev/SubZeroDev.WinGet',
            label: 'GitHub',
            position: 'right',
          },
          {
            href: 'https://github.com/The-Running-Dev/SubZeroDev.WinGet/pkgs/nuget/SubZeroDev.WinGet',
            label: 'NuGet',
            position: 'right',
          },
        ],
      },
      footer: {
        style: 'dark',
        links: [
          {
            title: 'Docs',
            items: [
              {label: 'Introduction', to: '/'},
              {label: 'Getting Started', to: '/getting-started'},
              {label: 'Architecture', to: '/architecture'},
            ],
          },
          {
            title: 'Project',
            items: [
              {
                label: 'GitHub',
                href: 'https://github.com/The-Running-Dev/SubZeroDev.WinGet',
              },
              {
                label: 'Specification',
                href: 'https://github.com/The-Running-Dev/SubZeroDev.WinGet/blob/main/SPECIFICATION.md',
              },
              {
                label: 'Roadmap',
                href: 'https://github.com/The-Running-Dev/SubZeroDev.WinGet/blob/main/ROADMAP.md',
              },
            ],
          },
        ],
        copyright: `Copyright © ${new Date().getFullYear()} Ben Richards. Built with Docusaurus.`,
      },
      prism: {
        theme: prismThemes.github,
        darkTheme: prismThemes.dracula,
      },
    }),
};

export default config;
