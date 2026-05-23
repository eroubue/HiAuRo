import { defineComponent, h } from 'vue';

export const GameHeart = defineComponent({
  name: 'GameHeart',
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
        h('path', {"d": "M2.23594 3.23807C1.31801 4.91711 3.96703 7.49999 4.99252 7.50001C6.02941 7.50004 8.68725 4.91386 7.76178 3.23489C7.42304 2.62035 6.6076 2.42109 5.93938 2.6353C5.08723 2.90846 5.18961 3.50003 4.99253 3.50001C4.80522 3.49999 4.90947 2.90951 4.0603 2.63604C3.39103 2.4205 2.57323 2.62113 2.23594 3.23807Z", "fillRule": "evenodd"})
      ]
    );
  }
});
