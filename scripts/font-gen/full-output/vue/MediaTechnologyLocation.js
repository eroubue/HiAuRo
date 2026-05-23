import { defineComponent, h } from 'vue';

export const MediaTechnologyLocation = defineComponent({
  name: 'MediaTechnologyLocation',
  props: {
    class: {
      type: String,
      default: ''
    }
  },
  setup(props, { attrs }) {
    return () => h(
      'svg',
      {
        viewBox: '0 0 20 20',
        
        class: `game-icons ${props.class}`,
        ...attrs
      },
      [
        h('path', {"d": "M5 1.93552C6.4 1.93552 7.2002 2.73571 7.2002 4.13571C7.20003 4.68552 6.86078 5.54357 6.44876 6.38304C5.92033 7.45966 5.65612 7.99797 5.11122 8.05895C5.04515 8.06635 4.95485 8.06635 4.88878 8.05895C4.34388 7.99797 4.07967 7.45966 3.55124 6.38304C3.13922 5.54357 2.79997 4.68552 2.7998 4.13571C2.7998 2.73571 3.6 1.93552 5 1.93552ZM5 3.03513C4.44778 3.03513 4.00011 3.48293 4 4.03513C4 4.58741 4.44772 5.03513 5 5.03513C5.55228 5.03513 6 4.58741 6 4.03513C5.99989 3.48293 5.55222 3.03513 5 3.03513Z", "fillRule": "evenodd"})
      ]
    );
  }
});
